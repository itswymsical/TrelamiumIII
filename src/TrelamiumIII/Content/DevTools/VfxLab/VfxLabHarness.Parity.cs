using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ReLogic.Content;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Vfx;

namespace TrelamiumIII.Content.DevTools.VfxLab;

/// <summary>
///     Holds the generator to what's already tuned: every look drawn through <c>GasFbm.fx</c>, rebuilt as a stack of stages, has to
///     produce the same pixels as the shader it replaces. Off screen over a synthetic field, where any difference at all is real;
///     then on screen through the real field path, with <see cref="IceMistField" /> on either side as a control.
/// </summary>
public sealed partial class VfxLabHarness
{
    private const int ParityWidth = 800;
    private const int ParityHeight = 450;

    /// <summary> Blobs in the synthetic field, enough for thin edges, deep overlaps and empty space. </summary>
    private const int ParityBlobs = 40;

    /// <summary> Rounding between two compilations of the same maths can turn up as this much out of 255. </summary>
    private const int ParityTolerance = 1;

    /// <summary> What a pixel may differ by on screen before it counts as different, for the slight drift of the world behind the field. </summary>
    private const int ScreenTolerance = 3;

    private const int ScreenSettleTicks = 30;

    private static readonly PropertyInfo LookProperty = typeof(FieldLayer).GetProperty("Look", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly PropertyInfo ShaderProperty = typeof(FieldLayer).GetProperty("Shader", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary> Every path through the shader that the mod's real looks might not use: a surface line, heat, stretch, flow, few bands. </summary>
    private static readonly GasLook TortureLook = new()
    {
        Noise = static () => Assets.Textures.Noise.Turbulence_06.Asset,
        WorldLight = new Vector3(0.3f, 0.2f, 0.1f),
        Threshold = 0.3f,
        DepthRange = 0.6f,
        NoiseScale = 2.3f,
        NoiseStrength = 0.5f,
        Drift = new Vector2(-0.13f, 0.07f),
        NoiseStretch = new Vector2(1f, 0.45f),
        FlowStrength = 0.4f,
        Bands = 5f,
        BodyAlpha = 0.9f,
        BodyAlphaFloor = 0.4f,
        Shadow = new Vector3(0.1f, 0.2f, 0.3f),
        Body = new Vector3(0.3f, 0.5f, 0.2f),
        Core = new Vector3(0.9f, 0.8f, 0.6f),
        Outline = new Vector3(0.05f, 0.1f, 0.2f),
        OutlineAlpha = 0.6f,
        Highlight = new Vector3(1f, 0.9f, 0.7f),
        HighlightAlpha = 0.7f,
        TemperatureBlend = 1.5f,
        HotShadow = new Vector3(0.5f, 0.1f, 0.05f),
        HotBody = new Vector3(0.9f, 0.4f, 0.1f),
        HotCore = new Vector3(1f, 0.9f, 0.5f),
    };

    private static IEnumerable<bool> ParityScript()
    {
        StageLibrary library = LabFiles.LoadBuiltLibrary(harnessMod!);

        Log($"library out of the .tmod: {library.Stages.Count} stages, {library.Hosts.Count} hosts");

        foreach (string problem in library.Problems)
            Log($"FAIL: library problem: {problem}");

        waitTicks = SettleTicks;
        yield return true;

        Asset<Effect> gasFbm = Assets.Shaders.GasFbm.Asset;

        List<(string Name, GasLook Look)> looks = [("Torture", TortureLook)];

        foreach (FieldLayer layer in FieldSystem.Layers)
        {
            bool sharesShader = ShaderProperty.GetValue(layer) is Asset<Effect> shader && shader.Name == gasFbm.Name;

            if (sharesShader && LookProperty.GetValue(layer) is GasLook look)
                looks.Add((layer.GetType().Name, look));
            else
                Log($"not compared: {layer.GetType().Name} ({(sharesShader ? "no look" : "its own shader")})");
        }

        int failed = 0;

        foreach ((string name, GasLook look) in looks)
        {
            VfxRecipe recipe = LookRecipe.From(name, look);
            GeneratedShader generated = ShaderGenerator.Generate(recipe, library);

            foreach (GenerationProblem problem in generated.Problems)
                Log($"{name}: {problem}");

            if (generated.Source is not { } source)
            {
                Log($"FAIL: {name} didn't generate");
                failed++;
                continue;
            }

            ShaderCompileResult compiled = default;
            foreach (bool step in Compile(source, result => compiled = result)) yield return step;

            if (compiled.Code is not { } code)
            {
                Log($"FAIL: {name} didn't compile: {compiled.Log}");
                failed++;
                continue;
            }

            if (compiled.Log.Length > 0)
                Log($"{name}: compiler said [{compiled.Log}]");

            bool same = false;
            foreach (bool step in RunOnDraw(() => same = CompareOffscreen(name, look, code, generated))) yield return step;

            if (!same)
                failed++;
        }

        Log(failed == 0 ? $"PASS: all {looks.Count} looks match GasFbm off screen" : $"FAIL: {failed} of {looks.Count} looks differ from GasFbm off screen");

        foreach (bool step in SensitivityCheck(library)) yield return step;
        foreach (bool step in OnScreenComparison(library)) yield return step;
    }

    /// <summary>
    ///     A comparison that can't fail proves nothing. Nudges every value of the look that uses every path through the shader, one
    ///     at a time, and each nudge has to change the picture: this catches a broken comparison, and a value that never reaches
    ///     the shader.
    /// </summary>
    private static IEnumerable<bool> SensitivityCheck(StageLibrary library)
    {
        GeneratedShader generated = ShaderGenerator.Generate(LookRecipe.From("Torture", TortureLook), library);

        ShaderCompileResult compiled = default;
        foreach (bool step in Compile(generated.Source!, result => compiled = result)) yield return step;

        foreach (bool step in RunOnDraw(() => CompareNudges(generated, compiled.Code!))) yield return step;
    }

    private static void CompareNudges(GeneratedShader generated, byte[] code)
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;

        using var target = new RenderTarget2D(device, ParityWidth, ParityHeight, false, SurfaceFormat.Color, DepthFormat.None);
        (Texture2D field, Texture2D attributes) = SyntheticField(device);
        using Texture2D fieldTexture = field;
        using Texture2D attributeTexture = attributes;
        using var made = new Effect(device, code);

        Color[] Draw(GeneratedShader shader)
        {
            ParityFrame(made, attributeTexture, 12.34f, Vector2.One, new Vector2(3.3f, 1.7f));
            LiveRecipe.BuildMaterial(shader).Apply(made);

            return Render(device, target, fieldTexture, made);
        }

        Color[] baseline = Draw(generated);

        List<string> missed = [];
        int caught = 0;

        for (int i = 0; i < generated.Uniforms.Count; i++)
        {
            UniformValue original = generated.Uniforms[i];

            UniformValue nudged = original.Kind == ParamKind.Noise
                ? original with { Texture = original.Texture == "Perlin" ? "Milky_2" : "Perlin" }
                : original with { Values = original.Values.Select(static value => value + 0.05f).ToArray() };

            List<UniformValue> uniforms = [.. generated.Uniforms];
            uniforms[i] = nudged;

            Color[] changed = Draw(generated with { Uniforms = uniforms });
            bool differs = false;

            for (int pixel = 0; pixel < baseline.Length && !differs; pixel++)
            {
                Color e = baseline[pixel];
                Color a = changed[pixel];

                differs = Math.Max(Math.Max(Math.Abs(e.R - a.R), Math.Abs(e.G - a.G)), Math.Max(Math.Abs(e.B - a.B), Math.Abs(e.A - a.A))) > ParityTolerance;
            }

            if (differs)
                caught++;
            else
                missed.Add(original.Name);
        }

        made.Parameters["uAttrTex"]?.SetValue((Texture?)null);

        Log(missed.Count == 0
            ? $"PASS: all {caught} nudged values changed the picture, so the comparison can fail and every value reaches the shader"
            : $"FAIL: {caught} nudges were caught, and these changed nothing: {string.Join(", ", missed)}");
    }

    /// <summary> Real nodes through the real field path: the real layer, then the generated one, then the real one again as the control. </summary>
    private static IEnumerable<bool> OnScreenComparison(StageLibrary library)
    {
        IceMistField mist = FieldSystem.GetLayer<IceMistField>() ?? throw new InvalidOperationException("IceMistField wasn't registered.");
        GasLook look = (GasLook)LookProperty.GetValue(mist)!;

        // The recipe goes through a file and back, as a saved one would.
        VfxRecipe recipe = RecipeFile.Parse(RecipeFile.Serialize(LookRecipe.From("IceMist", look)));
        RecipeFile.Save(output!, recipe);

        var live = new LiveRecipe(slot!.Live, library);
        live.Set(recipe);

        while (live.Status is ShaderBuildStatus.Compiling)
        {
            live.Update();
            yield return true;
        }

        Log($"live recipe: {live.Status}; log [{live.Log}]");

        if (live.Status != ShaderBuildStatus.Compiled)
        {
            Log("FAIL: the live recipe didn't build");
            yield break;
        }

        File.WriteAllText(Path.Combine(output!, "IceMist.generated.fx"), ShaderGenerator.Generate(recipe, library).Source!);

        slot.Material = null;
        slot.ApplyStages = live.Material.Apply;

        // Everything that moves behind the field stands still, so the only thing left to differ is the field.
        frozenTime = Main.timeForVisualEffects;
        Main.windSpeedCurrent = Main.windSpeedTarget = 0f;

        Vector2 center = Main.LocalPlayer.Center;

        SpawnSkyGrid(mist, center);
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("parity_real")) yield return step;
        Color[] real = capturedPixels!;

        mist.ClearNodes();
        SpawnSkyGrid(slot, center);
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("parity_generated")) yield return step;
        Color[] generated = capturedPixels!;

        slot.ClearNodes();
        SpawnSkyGrid(mist, center);
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("parity_control")) yield return step;
        Color[] control = capturedPixels!;

        mist.ClearNodes();

        GraphicsDevice device = Main.instance.GraphicsDevice;
        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;

        // The grid's own patch of sky, above the ground and whoever's walking on it.
        var region = new Rectangle(width / 2 - 600, height / 2 - 400, 1200, 270);

        (double testMean, int testOver) = Difference(real, generated, width, region);
        (double controlMean, int controlOver) = Difference(real, control, width, region);

        Log($"on screen, generated vs real: mean {testMean:F3} of 255, {testOver} of {region.Width * region.Height} pixels over {ScreenTolerance}");
        Log($"on screen, real vs real (control): mean {controlMean:F3} of 255, {controlOver} pixels over {ScreenTolerance}");
        Log(testOver <= controlOver + region.Width * region.Height / 1000
            ? "PASS: generated field matches IceMistField on screen, to within what the real one differs from itself"
            : "FAIL: generated field differs from IceMistField on screen");

        // Eight times the difference, so anything that's there can be seen.
        PngWriter.Write(Path.Combine(output!, "vfx_parity_diff.png"), width, height, (x, y) =>
        {
            Color a = real[y * width + x];
            Color b = generated[y * width + x];

            return new Color(Math.Min(Math.Abs(a.R - b.R) * 8, 255), Math.Min(Math.Abs(a.G - b.G) * 8, 255), Math.Min(Math.Abs(a.B - b.B) * 8, 255));
        });

        frozenTime = null;
    }

    /// <summary> Pinned in the sky, away from any tile a real gas layer could leave a residue on, and each with its own velocity for the flow to read. </summary>
    private static void SpawnSkyGrid(FieldLayer layer, Vector2 center)
    {
        const int Columns = 23;
        const int Rows = 6;
        const float Spacing = 64f;

        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
            {
                Vector2 position = center + new Vector2((column - Columns / 2) * Spacing, -120f - row * Spacing);
                Vector2 velocity = new Vector2(MathF.Sin(column * 0.7f), MathF.Cos(row * 0.9f)) * 3f;
                float radius = 44f + 24f * (0.5f + 0.5f * MathF.Sin(column * 1.3f + row * 2.1f));

                layer.SpawnNode(position, velocity, radius, 1e7f, pinned: true);
            }
        }
    }

    private static (double Mean, int Over) Difference(Color[] a, Color[] b, int width, Rectangle region)
    {
        long sum = 0;
        int over = 0;

        for (int y = region.Top; y < region.Bottom; y++)
        {
            for (int x = region.Left; x < region.Right; x++)
            {
                Color first = a[y * width + x];
                Color second = b[y * width + x];

                int red = Math.Abs(first.R - second.R);
                int green = Math.Abs(first.G - second.G);
                int blue = Math.Abs(first.B - second.B);

                sum += red + green + blue;

                if (Math.Max(red, Math.Max(green, blue)) > ScreenTolerance)
                    over++;
            }
        }

        return (sum / (3d * region.Width * region.Height), over);
    }

    /// <summary> Draws one look through both shaders with nothing else involved and compares every channel of every pixel. </summary>
    private static bool CompareOffscreen(string name, GasLook look, byte[] code, GeneratedShader generated)
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;

        look.Noise().Wait?.Invoke();

        using var target = new RenderTarget2D(device, ParityWidth, ParityHeight, false, SurfaceFormat.Color, DepthFormat.None);
        (Texture2D field, Texture2D attributes) = SyntheticField(device);
        using Texture2D fieldTexture = field;
        using Texture2D attributeTexture = attributes;
        using var made = new Effect(device, code);

        Effect real = Assets.Shaders.GasFbm.Asset.Value;
        VfxMaterial material = LiveRecipe.BuildMaterial(generated);
        Vector3 floor = look.WorldLight ?? Vector3.Zero;

        int worst = 0;
        int over = 0;
        int drawn = 0;

        // Two frames of everything the host takes from outside, so a uniform that only agrees by luck gets caught.
        foreach ((float time, Vector2 zoom, Vector2 offset) in new[] { (12.34f, Vector2.One, new Vector2(3.3f, 1.7f)), (87.5f, new Vector2(1.3f), new Vector2(-0.4f, 9.2f)) })
        {
            ParityFrame(real, attributeTexture, time, zoom, offset);
            look.Apply(real);
            real.Parameters["WorldLight"]?.SetValue(floor);
            Color[] expected = Render(device, target, fieldTexture, real);

            ParityFrame(made, attributeTexture, time, zoom, offset);
            material.Apply(made);
            Color[] actual = Render(device, target, fieldTexture, made);

            for (int i = 0; i < expected.Length; i++)
            {
                Color e = expected[i];
                Color a = actual[i];
                int difference = Math.Max(Math.Max(Math.Abs(e.R - a.R), Math.Abs(e.G - a.G)), Math.Max(Math.Abs(e.B - a.B), Math.Abs(e.A - a.A)));

                worst = Math.Max(worst, difference);
                drawn += e.A > 0 ? 1 : 0;

                if (difference > ParityTolerance)
                    over++;
            }

            real.Parameters["uAttrTex"]?.SetValue((Texture?)null);
            made.Parameters["uAttrTex"]?.SetValue((Texture?)null);
        }

        bool same = over == 0 && drawn > 0;

        Log($"{(same ? "PASS" : "FAIL")}: {name}: worst channel difference {worst}/255, {over} pixels over {ParityTolerance}, {drawn} drawn pixels across both frames, noise {generated.Uniforms.First(static uniform => uniform.Kind == ParamKind.Noise).Texture}");

        return same;
    }

    /// <summary> Everything a shader takes from its host, set the same way for whichever shaders are being compared. </summary>
    private static void ParityFrame(Effect effect, Texture2D attributes, float time, Vector2 zoom, Vector2 offset)
    {
        FieldLayer.ApplyScreenParameters(effect, new Vector2(2f));
        effect.Parameters["Time"]?.SetValue(time);
        effect.Parameters["Aspect"]?.SetValue(ParityWidth / (float)ParityHeight);
        effect.Parameters["Zoom"]?.SetValue(zoom);
        effect.Parameters["WorldOffset"]?.SetValue(offset);
        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(1f / ParityWidth, 1f / ParityHeight));
        effect.Parameters["uAttrTex"]?.SetValue(attributes);
    }

    private static Color[] Render(GraphicsDevice device, RenderTarget2D target, Texture2D field, Effect effect)
    {
        SpriteBatch batch = Main.spriteBatch;

        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);

        // Opaque, so what's compared is exactly what each shader returned rather than that blended over something.
        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        effect.CurrentTechnique.Passes[0].Apply();
        batch.Draw(field, new Rectangle(0, 0, ParityWidth, ParityHeight), Color.White);
        batch.End();

        device.SetRenderTarget(null);

        var pixels = new Color[ParityWidth * ParityHeight];
        target.GetData(pixels);

        return pixels;
    }

    /// <summary>
    ///     A field with something in every channel: overlapping blobs for coverage past one, brightness down the screen, temperature
    ///     across it, and flow in both directions. Held in floats, like the real targets, so coverage can run past one.
    /// </summary>
    private static (Texture2D Field, Texture2D Attributes) SyntheticField(GraphicsDevice device)
    {
        var field = new Vector4[ParityWidth * ParityHeight];
        var attributes = new Vector4[ParityWidth * ParityHeight];
        var blobs = new (Vector2 Center, float Radius, float Weight)[ParityBlobs];
        float aspect = ParityWidth / (float)ParityHeight;

        for (int i = 0; i < ParityBlobs; i++)
        {
            float x = (i * 0.381966f) % 1f;
            float y = (i * 0.618034f) % 1f;

            blobs[i] = (new Vector2(x * aspect, y), 0.05f + 0.03f * (i % 5), 0.6f + 0.2f * (i % 3));
        }

        for (int py = 0; py < ParityHeight; py++)
        {
            float v = (py + 0.5f) / ParityHeight;

            for (int px = 0; px < ParityWidth; px++)
            {
                float u = (px + 0.5f) / ParityWidth;
                var point = new Vector2(u * aspect, v);
                float coverage = 0f;

                foreach ((Vector2 center, float radius, float weight) in blobs)
                {
                    float falloff = MathHelper.Clamp(1f - Vector2.Distance(point, center) / radius, 0f, 1f);
                    coverage += falloff * falloff * weight;
                }

                float flowX = 0.5f + 0.5f * MathF.Sin(u * 12f);
                float flowY = 0.5f + 0.5f * MathF.Cos(v * 9f);

                field[py * ParityWidth + px] = new Vector4(coverage, coverage * (0.2f + 0.8f * v), coverage * u, coverage * ((u * 3f + v) % 1f));
                attributes[py * ParityWidth + px] = new Vector4(coverage * flowX, coverage * flowY, coverage * v, coverage * u);
            }
        }

        var fieldTexture = new Texture2D(device, ParityWidth, ParityHeight, false, SurfaceFormat.Vector4);
        fieldTexture.SetData(field);

        var attributeTexture = new Texture2D(device, ParityWidth, ParityHeight, false, SurfaceFormat.Vector4);
        attributeTexture.SetData(attributes);

        return (fieldTexture, attributeTexture);
    }
}
