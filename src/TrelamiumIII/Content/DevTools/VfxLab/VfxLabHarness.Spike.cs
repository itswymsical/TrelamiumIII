using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ReLogic.Content;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.DevTools.VfxLab;

/// <summary>
///     Proves a shader compiled while the game runs can replace the one a real <see cref="FieldLayer" /> draws with: that it shows
///     up, survives being swapped hundreds of times, and what the swap and a full stack of stages cost.
/// </summary>
public sealed partial class VfxLabHarness
{
    private const int BaselineTicks = 120;
    private const int ShowTicks = 12;

    /// <summary> Frames after a swap to search for the hitch: the swap's own frame and the ones that may bind the new shader late. </summary>
    private const int HitchFrames = 3;

    private const int CompileSwaps = 30;
    private const int RapidSwaps = 300;
    private const int RapidBatches = 3;

    /// <summary> A 1600x900 window at the field's PixelScale of 2. </summary>
    private const int BenchWidth = 800;

    private const int BenchHeight = 450;
    private const int BenchDraws = 40;
    private const int BenchRounds = 5;

    /// <summary> Dim on purpose: far enough apart for the check, and not bright enough to hurt. </summary>
    private const string DimRed = "float3(0.30, 0.12, 0.12)";

    private const string DimBlue = "float3(0.12, 0.12, 0.30)";

    private const string SolidTemplate = """
        sampler2D FieldSampler : register(s0);
        float FieldGain;

        float4 LabPass(float2 uv : TEXCOORD0) : COLOR0
        {
            float f = tex2D(FieldSampler, uv).r * FieldGain;
            if (f * f < 0.2)
                discard;
            return float4(COLOUR, 1.0);
        }

        technique Lab { pass P0 { PixelShader = compile ps_3_0 LabPass(); } }
        """;

    private const string BrokenSource = """
        #line 10 "Stages/Warp.stage.fxh"
        float4 LabPass(float2 uv : TEXCOORD0) : COLOR0 { return nope; }
        technique Lab { pass P0 { PixelShader = compile ps_3_0 LabPass(); } }
        """;

    /// <summary>
    ///     Stands in for a heavy recipe: the field host, then warp, three four-octave noise layers, rim, and two ramps blended by
    ///     node temperature. Uses every sampler and signal such a recipe would.
    /// </summary>
    private const string FullStack = """
        sampler2D FieldSampler : register(s0);
        texture2D uAttrTex;
        sampler2D AttrSampler = sampler_state { Texture = <uAttrTex>; AddressU = Clamp; AddressV = Clamp; MinFilter = Point; MagFilter = Point; };

        #define NOISE(n) texture2D n##Tex; sampler2D n##S = sampler_state { Texture = <n##Tex>; AddressU = Wrap; AddressV = Wrap; MinFilter = Linear; MagFilter = Linear; };
        #define RAMP(n) texture2D n##Tex; sampler2D n##S = sampler_state { Texture = <n##Tex>; AddressU = Clamp; AddressV = Clamp; MinFilter = Point; MagFilter = Point; };

        NOISE(warp_Noise) NOISE(noiseA_Noise) NOISE(noiseB_Noise) NOISE(pattern_Noise) RAMP(ramp_A) RAMP(ramp_B)

        float Time;
        float2 FieldTexel;
        float2 WorldOffset;
        float Aspect;
        float2 Zoom;
        float FieldGain;
        float3 WorldLight;
        float Threshold;
        float DepthRange;
        float Bands;
        float BodyAlpha;
        float BodyAlphaFloor;
        float3 OutlineColor;
        float OutlineAlpha;
        float3 HighlightColor;
        float HighlightAlpha;
        float FlowStrength;

        float warp_Amount;
        float warp_Scale;
        float2 warp_Drift;
        float noiseA_Scale;
        float noiseA_Strength;
        float2 noiseA_Drift;
        float noiseB_Scale;
        float noiseB_Strength;
        float2 noiseB_Drift;
        float pattern_Scale;
        float pattern_Amount;
        float ramp_Blend;

        float Cov(float2 uv) { return tex2Dlod(FieldSampler, float4(uv, 0.0, 0.0)).r; }
        float Den(float2 uv) { float f = Cov(uv) * FieldGain; return f * f; }

        float Fbm(sampler2D s, float2 p)
        {
            float t = 0.0;
            float a = 0.5;
            for (int i = 0; i < 4; i++)
            {
                t += tex2Dlod(s, float4(p, 0.0, 0.0)).r * a;
                p = p * 2.02 + 0.13;
                a *= 0.5;
            }
            return t;
        }

        float4 LabPass(float2 raw : TEXCOORD0) : COLOR0
        {
            float2 uv = (floor(raw / FieldTexel) + 0.5) * FieldTexel;
            float2 aspect = float2(Aspect, 1.0);
            float2 half_ = aspect * 0.5;
            float2 world = (uv * aspect - half_) / Zoom + half_ + WorldOffset;

            float4 field = tex2Dlod(FieldSampler, float4(uv, 0.0, 0.0));
            float4 attr = tex2Dlod(AttrSampler, float4(uv, 0.0, 0.0));
            float inv = 1.0 / max(field.r, 0.0001);
            float2 flow = attr.rg * inv * 2.0 - 1.0;
            float heat = field.b * inv;
            float3 light = max((field.g * inv).xxx, WorldLight);

            float2 p = world - flow * FlowStrength;

            float2 wp = p * warp_Scale + warp_Drift * Time;
            float2 warp = float2(Fbm(warp_NoiseS, wp), Fbm(warp_NoiseS, wp + float2(0.37, 0.71)));
            p += (warp - 0.5) * warp_Amount;

            float a = Fbm(noiseA_NoiseS, p * noiseA_Scale + noiseA_Drift * Time);
            float b = Fbm(noiseB_NoiseS, p * noiseB_Scale + noiseB_Drift * Time);
            float erosion = noiseA_Strength * (a - 0.5) + noiseB_Strength * (b - 0.5);

            float d = Den(uv) - erosion;
            if (d < Threshold)
                discard;

            float2 tx = float2(FieldTexel.x, 0.0);
            float2 ty = float2(0.0, FieldTexel.y);
            float up = Den(uv - ty) - erosion;
            if (HighlightAlpha > 0.0 && up < Threshold)
                return float4(HighlightColor * light * HighlightAlpha, HighlightAlpha);

            float side = min(Den(uv + ty), min(Den(uv - tx), Den(uv + tx))) - erosion;
            if (min(side, up) < Threshold)
                return float4(OutlineColor * light * OutlineAlpha, OutlineAlpha);

            float t = 1.0 - exp(-(d - Threshold) / max(DepthRange, 0.001));
            float shade = saturate(t * (0.55 + 0.9 * a));
            shade = floor(shade * Bands) / max(Bands - 1.0, 1.0);
            shade = saturate(shade + (Fbm(pattern_NoiseS, p * pattern_Scale) - 0.5) * pattern_Amount);

            float3 colour = lerp(
                tex2Dlod(ramp_AS, float4(shade, 0.5, 0.0, 0.0)).rgb,
                tex2Dlod(ramp_BS, float4(shade, 0.5, 0.0, 0.0)).rgb,
                saturate(heat * ramp_Blend));

            float alpha = BodyAlpha * lerp(BodyAlphaFloor, 1.0, shade);
            return float4(colour * light * alpha, alpha);
        }

        technique Lab { pass P0 { PixelShader = compile ps_3_0 LabPass(); } }
        """;

    private static readonly GasLook SolidLook = new();

    private static readonly GasLook StackLook = new()
    {
        WorldLight = Vector3.One,
        Threshold = 0.22f,
        DepthRange = 0.8f,
        Bands = 8f,
        BodyAlpha = 0.85f,
        BodyAlphaFloor = 0.6f,
        OutlineAlpha = 0.9f,
        Outline = new Vector3(0.1f, 0.12f, 0.08f),
        FlowStrength = 0.2f,
    };

    private static Texture2D? rampA;
    private static Texture2D? rampB;
    private static Asset<Texture2D>? warpNoise;
    private static Asset<Texture2D>? noiseA;
    private static Asset<Texture2D>? noiseB;
    private static Asset<Texture2D>? patternNoise;

    private static byte[]? solidCode;
    private static byte[]? stackCode;

    private static string Solid(string colour) => SolidTemplate.Replace("COLOUR", colour);

    /// <summary> A comment is enough to make the compiler's cache miss and the source a new shader. </summary>
    private static string Salted(string salt) => $"// salt {salt}\n{FullStack}";

    private static IEnumerable<bool> SpikeScript()
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;

        Log($"driver override: {Environment.GetEnvironmentVariable("FNA3D_FORCE_DRIVER") ?? "none"}");
        Log($"adapter: {device.Adapter.Description}, backbuffer {device.PresentationParameters.BackBufferWidth}x{device.PresentationParameters.BackBufferHeight}");
        Log($"fxc: {ShaderCompiler.FxcPath ?? "NOT FOUND"}");

        warpNoise = ModContent.Request<Texture2D>(Assets.Textures.Noise.Milky_2.KEY, AssetRequestMode.ImmediateLoad);
        noiseA = ModContent.Request<Texture2D>(Assets.Textures.Noise.Milky_3.KEY, AssetRequestMode.ImmediateLoad);
        noiseB = ModContent.Request<Texture2D>(Assets.Textures.Noise.Perlin.KEY, AssetRequestMode.ImmediateLoad);
        patternNoise = ModContent.Request<Texture2D>(Assets.Textures.Noise.VoronoiNoise.KEY, AssetRequestMode.ImmediateLoad);

        rampA = BuildRamp(new Vector3(0.10f, 0.20f, 0.10f), new Vector3(0.35f, 0.60f, 0.28f), new Vector3(0.75f, 0.90f, 0.55f));
        rampB = BuildRamp(new Vector3(0.30f, 0.06f, 0.02f), new Vector3(0.85f, 0.40f, 0.10f), new Vector3(1.00f, 0.90f, 0.55f));

        waitTicks = SettleTicks;
        yield return true;

        // With no effect yet the slot has to skip its composite rather than crash.
        SpawnGrid();
        waitTicks = BaselineTicks;
        frames.Clear();
        yield return true;

        Log($"frame ms, no effect (nodes spawned): {Summary(frames)}");

        foreach (bool step in Capture("none")) yield return step;
        Vector3 none = capturedMean;

        // Each of these has to reach the screen: a colour nothing else on it has.
        ShaderCompileResult red = default;
        foreach (bool step in Compile(Solid(DimRed), result => red = result)) yield return step;
        Log($"red: compiled {red.Code?.Length ?? 0} bytes in {red.Milliseconds:F0} ms; log [{red.Log}]");
        solidCode = red.Code ?? throw new InvalidOperationException("red didn't compile");

        slot!.Material = SolidLook;
        slot.ApplyStages = null;
        Log($"red: new Effect + swap {Swap(solidCode):F2} ms");
        waitTicks = ShowTicks;
        yield return true;
        foreach (bool step in Capture("red")) yield return step;
        Vector3 redMean = capturedMean;

        ShaderCompileResult blue = default;
        foreach (bool step in Compile(Solid(DimBlue), result => blue = result)) yield return step;
        Log($"blue: compiled {blue.Code?.Length ?? 0} bytes in {blue.Milliseconds:F0} ms");
        Log($"blue: new Effect + swap {Swap(blue.Code!):F2} ms");
        waitTicks = ShowTicks;
        yield return true;
        foreach (bool step in Capture("blue")) yield return step;
        Vector3 blueMean = capturedMean;

        Log($"screen mean (r,g,b): none {none}, red {redMean}, blue {blueMean}");
        Log(redMean.X > redMean.Z + 0.1f && blueMean.Z > blueMean.X + 0.1f
            ? "PASS: both swaps reached the screen"
            : "FAIL: a swap didn't reach the screen");

        // A cached source has to come straight back.
        ShaderCompileResult again = default;
        foreach (bool step in Compile(Solid(DimBlue), result => again = result)) yield return step;
        Log($"cache: second compile of blue took {again.Milliseconds:F1} ms, cached {again.Cached}");
        Log(again.Cached && again.Code is not null && again.Code.SequenceEqual(blue.Code!) ? "PASS: cache hit" : "FAIL: cache missed");

        // The full stack.
        string runId = DateTime.UtcNow.Ticks.ToString();
        ShaderCompileResult stack = default;
        foreach (bool step in Compile(Salted(runId), result => stack = result)) yield return step;
        Log($"stack: compiled {stack.Code?.Length ?? 0} bytes in {stack.Milliseconds:F0} ms; log [{stack.Log}]");
        stackCode = stack.Code ?? throw new InvalidOperationException("the full stack didn't compile");

        slot.Material = StackLook;
        slot.ApplyStages = ApplyStack;
        int mark = frames.Count;
        Log($"stack: new Effect + swap {Swap(stackCode):F2} ms");
        while (frames.Count < mark + HitchFrames) yield return true;
        Log($"stack: frames after first swap {string.Join(", ", frames.Skip(mark).Take(HitchFrames).Select(static ms => ms.ToString("F1")))} ms");
        waitTicks = ShowTicks;
        yield return true;
        foreach (bool step in Capture("stack")) yield return step;
        Log($"stack: screen mean {capturedMean}");

        frames.Clear();
        waitTicks = BaselineTicks;
        yield return true;
        double baseline = Median(frames);
        Log($"frame ms, stack on screen: {Summary(frames)}");

        // A compile that fails has to leave the running shader alone and say where it went wrong.
        Effect? before = slot.Live.Asset.Value;
        ShaderCompileResult broken = default;
        foreach (bool step in Compile(BrokenSource, result => broken = result)) yield return step;
        Log($"broken: code null {broken.Code is null}, log [{broken.Log}]");
        Log(broken.Code is null && broken.Log.Contains("Stages/Warp.stage.fxh(10", StringComparison.Ordinal) ? "PASS: error names the stage file and line" : "FAIL: error doesn't name the stage");
        Log(ReferenceEquals(before, slot.Live.Asset.Value) ? "PASS: failed compile left the shader running" : "FAIL: failed compile changed the shader");

        // Live editing: compile a new source, swap it in, keep drawing.
        List<double> compileTimes = [];
        List<double> swapTimes = [];
        List<double> hitches = [];
        List<double> whileCompiling = [];

        for (int i = 0; i < CompileSwaps; i++)
        {
            int compileStart = frames.Count;

            ShaderCompileResult next = default;
            foreach (bool step in Compile(Salted($"{runId}-{i}"), result => next = result)) yield return step;

            whileCompiling.AddRange(frames.Skip(compileStart));
            compileTimes.Add(next.Milliseconds);
            mark = frames.Count;
            swapTimes.Add(Swap(next.Code ?? throw new InvalidOperationException($"swap {i} didn't compile")));

            while (frames.Count < mark + HitchFrames) yield return true;
            hitches.Add(frames.Skip(mark).Take(HitchFrames).Max());
        }

        Log($"{CompileSwaps} compile-and-swaps: compile ms {Summary(compileTimes)}");
        Log($"{CompileSwaps} compile-and-swaps: frame ms while compiling {Summary(whileCompiling)}");
        Log($"{CompileSwaps} compile-and-swaps: new Effect + swap ms {Summary(swapTimes)}");
        Log($"{CompileSwaps} compile-and-swaps: worst of {HitchFrames} frames after each swap, ms {Summary(hitches)} (baseline frame {baseline:F1})");

        // Disposal under load: a new effect every tick, while one is being drawn every frame. Two compiles of one look, because
        // two different looks here would strobe the whole screen at 30 Hz and the disposal doesn't care what they draw.
        ShaderCompileResult twin = default;
        foreach (bool step in Compile(Salted($"{runId}-twin"), result => twin = result)) yield return step;
        byte[] twinCode = twin.Code ?? throw new InvalidOperationException("the twin didn't compile");

        for (int batch = 0; batch < RapidBatches; batch++)
        {
            List<double> rapid = [];

            for (int i = 0; i < RapidSwaps; i++)
            {
                rapid.Add(Swap(i % 2 == 0 ? twinCode : stackCode));
                yield return true;
            }

            Swap(stackCode);
            waitTicks = 60;
            yield return true;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            using Process process = Process.GetCurrentProcess();
            Log($"rapid batch {batch + 1}: {RapidSwaps} swaps, ms {Summary(rapid)}, private memory {process.PrivateMemorySize64 / (1024 * 1024)} MB, managed {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        }

        foreach (bool step in Capture("stack_after_stress")) yield return step;
        Log($"after stress: screen mean {capturedMean}");

        foreach (bool step in RunOnDraw(Bench)) yield return step;
    }

    private static double Swap(byte[] code)
    {
        long start = Stopwatch.GetTimestamp();
        slot!.Live.Swap(code);

        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    /// <summary> Pinned, and with so much life they don't fade, so every frame draws the same thing. Temperature runs left to right, for the ramp blend. </summary>
    private static void SpawnGrid()
    {
        const int Columns = 23;
        const int Rows = 13;
        const float Spacing = 64f;

        Vector2 center = Main.LocalPlayer.Center;

        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
            {
                var offset = new Vector2((column - Columns / 2) * Spacing, (row - Rows / 2) * Spacing);

                slot!.SpawnNode(center + offset, Vector2.Zero, 90f, 1e7f, temperature: 1200f * column / (Columns - 1f), pinned: true);
            }
        }

        Log($"spawned {slot!.NodeCount} nodes");
    }

    private static Texture2D BuildRamp(Vector3 shadow, Vector3 body, Vector3 core)
    {
        var data = new Color[256];

        for (int i = 0; i < data.Length; i++)
        {
            float shade = i / 255f;

            data[i] = new Color(shade < 0.5f ? Vector3.Lerp(shadow, body, shade * 2f) : Vector3.Lerp(body, core, shade * 2f - 1f));
        }

        var ramp = new Texture2D(Main.instance.GraphicsDevice, data.Length, 1);
        ramp.SetData(data);

        return ramp;
    }

    private static void ApplyStack(Effect effect)
    {
        EffectParameterCollection parameters = effect.Parameters;

        parameters["warp_NoiseTex"]?.SetValue(warpNoise!.Value);
        parameters["noiseA_NoiseTex"]?.SetValue(noiseA!.Value);
        parameters["noiseB_NoiseTex"]?.SetValue(noiseB!.Value);
        parameters["pattern_NoiseTex"]?.SetValue(patternNoise!.Value);
        parameters["ramp_ATex"]?.SetValue(rampA);
        parameters["ramp_BTex"]?.SetValue(rampB);

        parameters["warp_Amount"]?.SetValue(0.25f);
        parameters["warp_Scale"]?.SetValue(0.8f);
        parameters["warp_Drift"]?.SetValue(new Vector2(0.02f, -0.01f));
        parameters["noiseA_Scale"]?.SetValue(1.4f);
        parameters["noiseA_Strength"]?.SetValue(0.35f);
        parameters["noiseA_Drift"]?.SetValue(new Vector2(0.03f, -0.05f));
        parameters["noiseB_Scale"]?.SetValue(3f);
        parameters["noiseB_Strength"]?.SetValue(0.2f);
        parameters["noiseB_Drift"]?.SetValue(new Vector2(-0.04f, -0.02f));
        parameters["pattern_Scale"]?.SetValue(6f);
        parameters["pattern_Amount"]?.SetValue(0.2f);
        parameters["ramp_Blend"]?.SetValue(1f);
    }

    /// <summary>
    ///     How long the GPU takes over a full field target of each shader, every pixel in the body of the material so nothing
    ///     exits early. Reading a pixel back makes the CPU wait for the draws, which a frame interval wouldn't show under vsync.
    /// </summary>
    private static void Bench()
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;
        SpriteBatch batch = Main.spriteBatch;

        using var target = new RenderTarget2D(device, BenchWidth, BenchHeight, false, SurfaceFormat.Color, DepthFormat.None);
        using var white = new Texture2D(device, 1, 1);
        white.SetData([Color.White]);
        using var solid = new Effect(device, solidCode!);
        using var stack = new Effect(device, stackCode!);

        var bench = new (string Name, Effect Effect, GasLook Look, Action<Effect>? Stages, List<double> Times)[]
        {
            ("solid", solid, SolidLook, null, []),
            // The noise settings of the full stack's first layer, or GasFbm's cache-friendly default scale flatters it.
            ("GasFbm", Assets.Shaders.GasFbm.Asset.Value, new GasLook { WorldLight = Vector3.One, NoiseScale = 1.4f, NoiseStrength = 0.35f, Noise = static () => noiseA! }, null, []),
            ("full stack", stack, StackLook, ApplyStack, []),
        };

        var readback = new Color[1];
        var corner = new Rectangle(0, 0, 1, 1);

        void Flush() => target.GetData(0, corner, readback, 0, 1);

        for (int round = 0; round < BenchRounds; round++)
        {
            foreach ((string _, Effect effect, GasLook look, Action<Effect>? stages, List<double> times) in bench)
            {
                device.SetRenderTarget(target);
                device.Clear(Color.Transparent);
                Flush();

                EffectParameterCollection parameters = effect.Parameters;
                parameters["Time"]?.SetValue(1f);
                parameters["Aspect"]?.SetValue(BenchWidth / (float)BenchHeight);
                parameters["Zoom"]?.SetValue(Vector2.One);
                parameters["WorldOffset"]?.SetValue(Vector2.Zero);
                parameters["FieldTexel"]?.SetValue(new Vector2(1f / BenchWidth, 1f / BenchHeight));
                parameters["FieldGain"]?.SetValue(1.25f);
                parameters["uAttrTex"]?.SetValue(white);
                look.Apply(effect);
                stages?.Invoke(effect);

                long start = Stopwatch.GetTimestamp();

                batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                effect.CurrentTechnique.Passes[0].Apply();

                for (int i = 0; i < BenchDraws; i++)
                    batch.Draw(white, new Rectangle(0, 0, BenchWidth, BenchHeight), Color.White);

                batch.End();
                Flush();

                times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds / BenchDraws);

                parameters["uAttrTex"]?.SetValue((Texture?)null);
            }
        }

        device.SetRenderTarget(null);

        foreach ((string name, Effect _, GasLook _, Action<Effect>? _, List<double> times) in bench)
            Log($"gpu bench, {name}: {Median(times):F3} ms per full {BenchWidth}x{BenchHeight} draw (all rounds {string.Join(", ", times.Select(static ms => ms.ToString("F3")))})");
    }
}
