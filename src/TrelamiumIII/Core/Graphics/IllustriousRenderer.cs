using ReLogic.Content;
using System.Collections.Generic;
using Terraria.GameContent;

namespace TrelamiumIII.Core.Graphics;

public static class IllustriousRenderer
{
    private class EasterEggObject
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public float Scale;
        public float Alpha;
        public int Type;

        public EasterEggObject(Vector2 position, Vector2 velocity, float scale)
        {
            Position = position;
            Velocity = velocity;
            Rotation = Main.rand.NextFloat(0, MathHelper.TwoPi);
            Scale = scale;
            Alpha = 0f;

            if (Main.rand.NextBool(1000000))
            {
                Type = 8;
            }
            else
            {
                Type = Main.rand.Next(8);
            }

            if (Type == 7)
            {
                Scale = 0.2f;
            }
        }

        public void Update()
        {
            Position += Velocity;

            Rotation += IllustriousRenderer.EASTER_EGG_ROTATION_SPEED;
            if (Rotation > MathHelper.TwoPi)
                Rotation -= MathHelper.TwoPi;

            if (Alpha < 1f)
                Alpha += 0.01f;
        }

        public Texture2D GetTexture()
        {
            return Type switch
            {   
                0 => Assets.Textures.VFX.Smoke_01.Asset.Value,
                1 => Assets.Textures.VFX.Smoke_02.Asset.Value,
                2 => Assets.Textures.VFX.Smoke_03.Asset.Value,
                3 => Assets.Textures.VFX.Smoke_01.Asset.Value,
                4 => Assets.Textures.VFX.Smoke_01.Asset.Value,
                5 => Assets.Textures.VFX.Smoke_01.Asset.Value,
                6 => Assets.Textures.VFX.Smoke_01.Asset.Value,
                7 => Assets.Textures.VFX.Smoke_01.Asset.Value,
                8 => Assets.Textures.VFX.Smoke_01.Asset.Value,

                _ => Assets.Textures.VFX.Smoke_01.Asset.Value
            };
        }
    }
    private class GalaxyObject
    {
        public Vector2 Position;
        public float Scale;
        public float Rotation;
        public float RotationSpeed;
        public float Alpha;
        public float Time;
        public Color TintColor;
        public float FadeIn;

        // Spiral arm data
        private Vector2[] spiralPoints;
        private float[] spiralAlphas;
        private const int SPIRAL_SEGMENTS = 24;
        private const int SPIRAL_ARMS = 3;

        public GalaxyObject(Vector2 position, float scale)
        {
            Position = position;
            Scale = scale * Main.rand.NextFloat(0.5f, 0.8f);
            Rotation = Main.rand.NextFloat(0, MathHelper.TwoPi);
            RotationSpeed = Main.rand.NextFloat(0.0002f, 0.0008f) * (Main.rand.NextBool() ? 1 : -1);
            Alpha = 0f;
            FadeIn = 1f;
            Time = 0f;

            // Random galaxy colors - purple, blue, teal variations
            TintColor = Main.rand.Next(4) switch
            {
                0 => new Color(0.6f, 0.3f, 1.0f),   // Purple
                1 => new Color(0.3f, 0.6f, 1.0f),   // Blue  
                2 => new Color(0.4f, 0.8f, 0.9f),   // Teal
                _ => new Color(0.8f, 0.4f, 1.0f)    // Pink
            };

            GenerateSpiralArms();
        }

        private void GenerateSpiralArms()
        {
            spiralPoints = new Vector2[SPIRAL_SEGMENTS * SPIRAL_ARMS];
            spiralAlphas = new float[SPIRAL_SEGMENTS * SPIRAL_ARMS];

            for (int arm = 0; arm < SPIRAL_ARMS; arm++)
            {
                float armOffset = (arm / (float)SPIRAL_ARMS) * MathHelper.TwoPi;

                for (int i = 0; i < SPIRAL_SEGMENTS; i++)
                {
                    int index = arm * SPIRAL_SEGMENTS + i;
                    float t = i / (float)SPIRAL_SEGMENTS;

                    // Spiral equation: radius increases with angle
                    float angle = armOffset + t * MathHelper.TwoPi * 2f; // 2 full rotations
                    float radius = t * Scale * 80f; // Max radius based on scale

                    // Create spiral shape
                    float spiralAngle = angle + radius * 0.02f;
                    spiralPoints[index] = new Vector2(
                        (float)Math.Cos(spiralAngle) * radius,
                        (float)Math.Sin(spiralAngle) * radius
                    );

                    // Fade towards outer edges
                    spiralAlphas[index] = 1f - (t * t); // Quadratic falloff
                }
            }
        }

        public void Update()
        {
            Time += 1f / 60f;
            Rotation += RotationSpeed;

            // Fade in effect
            if (FadeIn > 0f)
            {
                FadeIn -= 0.01f;
                Alpha = 1f - FadeIn;
            }
            else
            {
                Alpha = 1f;
            }

            // Slowly regenerate spiral arms for animation
            if (Main.GameUpdateCount % 30 == 0) // Every half second
            {
                GenerateSpiralArms();
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D glowTexture, Texture2D starTexture)
        {
            if (Alpha <= 0f) return;

            Matrix rotationMatrix = Matrix.CreateRotationZ(Rotation);

            // Draw central core (bright center)
            float coreScale = Scale * 0.8f;
            spriteBatch.Draw(
                glowTexture,
                Position,
                null,
                TintColor * Alpha * 0.9f,
                Rotation,
                new Vector2(glowTexture.Width / 2, glowTexture.Height / 2),
                coreScale,
                SpriteEffects.None,
                0f
            );

            // Draw spiral arms using multiple small sprites
            for (int i = 0; i < spiralPoints.Length; i += 2) // Skip every other point for performance
            {
                Vector2 rotatedPoint = Vector2.Transform(spiralPoints[i], rotationMatrix);
                Vector2 worldPos = Position + rotatedPoint;

                // Skip if off-screen (basic culling)
                if (worldPos.X < -50 || worldPos.X > Main.screenWidth + 50 ||
                    worldPos.Y < -50 || worldPos.Y > Main.screenHeight + 50)
                    continue;

                float armAlpha = spiralAlphas[i] * Alpha * 0.6f;
                float armScale = Scale * 0.15f * (spiralAlphas[i] + 0.2f);

                // Add some spiral animation
                float timeOffset = Time * 0.5f + (i * 0.1f);
                armAlpha *= (0.7f + 0.3f * (float)Math.Sin(timeOffset));

                spriteBatch.Draw(
                    starTexture,
                    worldPos,
                    null,
                    TintColor * armAlpha,
                    Rotation + (i * 0.2f),
                    new Vector2(starTexture.Width / 2, starTexture.Height / 2),
                    armScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // Draw outer glow halo
            float haloScale = Scale * 1.5f;
            spriteBatch.Draw(
                glowTexture,
                Position,
                null,
                TintColor * Alpha * 0.3f,
                Rotation * 0.5f,
                new Vector2(glowTexture.Width / 2, glowTexture.Height / 2),
                haloScale,
                SpriteEffects.None,
                0f
            );
        }

        public bool ShouldRemove()
        {
            // Remove if moved too far off screen
            return Position.X < -200 || Position.X > Main.screenWidth + 200 ||
                   Position.Y < -200 || Position.Y > Main.screenHeight + 200;
        }
    }

    private static List<Star> menuStars = new();
    private static List<EasterEggObject> easterEggObjects = new();
    private static List<Vector2> clickPositions = new();
    private static List<float> clickTimes = new();
    private static Dictionary<Star, Vector2> starVelocities = new();

    private static bool starsInitialized = false;
    private static float spaceDriftSpeed = 0.05f;
    private static float galaxyTime = 0f;

    private const float CLICK_LIFETIME = 60f;
    private const float BURST_RADIUS = 120f;
    private const float BURST_FORCE = 3f;
    private const float COLLISION_THRESHOLD = 8f;
    private const float EASTER_EGG_CHANCE = 0.05f;
    private const float EASTER_EGG_ROTATION_SPEED = 0.005f;
    private const float EASTER_EGG_DRIFT_SPEED = 0.2f;

    public static void Load()
    {
        menuStars = new List<Star>();
        easterEggObjects = new List<EasterEggObject>();
        clickPositions = new List<Vector2>();
        clickTimes = new List<float>();
        starVelocities = new Dictionary<Star, Vector2>();
        starsInitialized = false;
    }

    public static void Unload()
    {
        ClearResources();
    }

    public static void ClearResources()
    {
        menuStars?.Clear();
        easterEggObjects?.Clear();
        clickPositions?.Clear();
        clickTimes?.Clear();
        starVelocities?.Clear();
        starsInitialized = false;
    }

    private static void InitializeStars()
    {
        ClearResources();

        var starCount = Main.rand.Next(70, 100);
        for (var i = 0; i < starCount; i++)
        {
            CreateNewStar(true);
        }

        TryCreateEasterEgg(new Vector2(Main.rand.Next(Main.screenWidth), Main.rand.Next(Main.screenHeight)));
        starsInitialized = true;
    }

    public static void Update(bool isActive)
    {
        // If the renderer isn't supposed to be active, clean up and exit
        if (!isActive)
        {
            if (starsInitialized) ClearResources();
            return;
        }

        galaxyTime += 1f / 60f;
        if (galaxyTime > 1000f) galaxyTime -= 1000f;

        if (Main.mouseLeft && Main.mouseLeftRelease)
        {
            clickPositions.Add(new Vector2(Main.mouseX, Main.mouseY));
            clickTimes.Add(0f);
        }

        for (var i = clickTimes.Count - 1; i >= 0; i--)
        {
            clickTimes[i]++;
            if (clickTimes[i] >= CLICK_LIFETIME)
            {
                clickTimes.RemoveAt(i);
                clickPositions.RemoveAt(i);
            }
        }
    }

    public static void DrawForMenu(SpriteBatch spriteBatch, Vector2 logoDrawCenter, float logoScale, float logoRotation, Texture2D logoTexture)
    {
        if (!starsInitialized)
            InitializeStars();

        var drawColor = Color.White;

        // 1. Draw Space Background
        spriteBatch.Draw(
           Assets.Textures.VFX.Noise.Noise1.Asset.Value,
            new Rectangle(0, 0, Main.screenWidth + 1, Main.screenHeight),
            drawColor
        );

        var glowTexture = Assets.Textures.VFX.Glow.Asset.Value;
        var starTexture = Assets.Textures.VFX.GlowFlare.Asset.Value;

        // Screen Glow
        spriteBatch.Draw(
            glowTexture,
            logoDrawCenter,
            null,
            drawColor * 0.02f,
            0f,
            new Vector2(glowTexture.Width / 2, glowTexture.Height / 2),
            new Vector2(logoTexture?.Width ?? 100, logoTexture?.Height ?? 100) * logoScale,
            SpriteEffects.None,
            0f
        );

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

        var colorStart = new Color(143, 244, 255);
        var colorEnd = Color.White;

        // 2. Process and Draw Easter Eggs
        for (var i = easterEggObjects.Count - 1; i >= 0; i--)
        {
            var easterEgg = easterEggObjects[i];
            easterEgg.Update();

            var texture = easterEgg.GetTexture();

            spriteBatch.Draw(glowTexture, easterEgg.Position, null, Color.White * 0.5f * easterEgg.Alpha, 0f, new Vector2(glowTexture.Width / 2, glowTexture.Height / 2), easterEgg.Scale * 2f, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, easterEgg.Position, null, Color.White * easterEgg.Alpha, easterEgg.Rotation, new Vector2(texture.Width / 2, texture.Height / 2), easterEgg.Scale, SpriteEffects.None, 0f);

            if (easterEgg.Position.X < -50 || easterEgg.Position.Y < -50 || easterEgg.Position.Y > Main.screenHeight + 50)
            {
                easterEggObjects.RemoveAt(i);
            }
        }

        // 3. Process Clicks and Star Forces
        ProcessClickBursts(glowTexture);

        // 4. Process Star Collisions
        ProcessStarCollisions(colorStart, colorEnd);

        // 5. Update and Draw Stars
        ProcessAndDrawStars(spriteBatch, glowTexture, starTexture, colorStart, colorEnd);

        if (Main.rand.NextBool(160))
            CreateNewStar();

        // 6. Draw Logo (Optional, passed in from Menu)
        if (logoTexture != null)
        {
            spriteBatch.Draw(logoTexture, new Vector2(logoDrawCenter.X / 1.35f, logoDrawCenter.Y * -0.25f), null, Color.White, logoRotation, Vector2.Zero, logoScale, SpriteEffects.None, 0f);
        }

        spriteBatch.End();
        spriteBatch.Begin();
    }
    public static void DrawScreenOverlay(SpriteBatch spriteBatch, float opacity = 1f, bool drawBackground = false)
    {
        if (!starsInitialized)
            InitializeStars();

        var glowTexture = Assets.Textures.VFX.Glow.Asset.Value;
        var starTexture = Assets.Textures.VFX.GlowFlare.Asset.Value;

        // 1. Draw Space Background (Optional for overlays)
        if (drawBackground)
        {
            spriteBatch.Draw(
                Assets.Textures.VFX.Noise.Noise1.Asset.Value,
                new Rectangle(0, 0, Main.screenWidth + 1, Main.screenHeight),
                Color.White * opacity
            );
        }

        spriteBatch.End();

        // Switch to Additive blending for the glowing stars and effects
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

        var colorStart = new Color(143, 244, 255);
        var colorEnd = Color.White;

        // 2. Process and Draw Easter Eggs
        for (var i = easterEggObjects.Count - 1; i >= 0; i--)
        {
            var easterEgg = easterEggObjects[i];
            easterEgg.Update();

            var texture = easterEgg.GetTexture();

            // Multiply colors by the opacity parameter to allow fading
            spriteBatch.Draw(glowTexture, easterEgg.Position, null, Color.White * 0.5f * easterEgg.Alpha * opacity, 0f, new Vector2(glowTexture.Width / 2, glowTexture.Height / 2), easterEgg.Scale * 2f, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, easterEgg.Position, null, Color.White * easterEgg.Alpha * opacity, easterEgg.Rotation, new Vector2(texture.Width / 2, texture.Height / 2), easterEgg.Scale, SpriteEffects.None, 0f);

            if (easterEgg.Position.X < -50 || easterEgg.Position.Y < -50 || easterEgg.Position.Y > Main.screenHeight + 50)
            {
                easterEggObjects.RemoveAt(i);
            }
        }

        // 3. Update and Draw Stars
        // NOTE: You will need to update your ProcessAndDrawStars method to accept the 'opacity' float 
        // and multiply the final star color by it, like this: var starColor = baseColor * brightness * alpha * opacity;
        ProcessAndDrawStars(spriteBatch, glowTexture, starTexture, colorStart, colorEnd);

        if (Main.rand.NextBool(160))
            CreateNewStar();

        spriteBatch.End();

        // 4. RESTORE THE SPRITEBATCH
        // This is crucial! It returns the spritebatch to Terraria's default UI state so the rest of the game doesn't break.
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
    }

    // --- Private Helper Methods Below ---

    private static void ProcessClickBursts(Texture2D glowTexture)
    {
        for (var i = 0; i < clickPositions.Count; i++)
        {
            var clickPos = clickPositions[i];
            var clickAge = clickTimes[i];
            var burstScale = clickAge / 15f * 3f;
            var alpha = 1f - clickAge / CLICK_LIFETIME;

            Main.spriteBatch.Draw(glowTexture, clickPos, null, new Color(179, 255, 243) * alpha * 0.5f, 0f, new Vector2(glowTexture.Width / 2, glowTexture.Height / 2), burstScale, SpriteEffects.None, 0f);

            if (clickAge < 2f)
            {
                foreach (var star in menuStars)
                {
                    if (star.falling) continue;
                    var distance = Vector2.Distance(clickPos, star.position);
                    if (distance < BURST_RADIUS)
                    {
                        var direction = star.position - clickPos;
                        if (direction != Vector2.Zero) direction.Normalize();
                        var force = BURST_FORCE * (1f - distance / BURST_RADIUS);
                        starVelocities[star] += direction * force;
                    }
                }
            }
        }
    }

    private static void ProcessStarCollisions(Color colorStart, Color colorEnd)
    {
        for (var i = 0; i < menuStars.Count; i++)
        {
            var star1 = menuStars[i];
            if (star1.falling) continue;

            for (var j = i + 1; j < menuStars.Count; j++)
            {
                var star2 = menuStars[j];
                if (star2.falling) continue;

                var distance = Vector2.Distance(star1.position, star2.position);
                var collisionSize = (star1.scale + star2.scale) * COLLISION_THRESHOLD;

                if (distance < collisionSize)
                {
                    var collisionPoint = (star1.position + star2.position) * 0.5f;
                    var colorLerp1 = (star1.type / 4f + (int)star1.position.X * (int)star1.position.Y % 10 / 10f) % 1f;
                    var colorLerp2 = (star2.type / 4f + (int)star2.position.X * (int)star2.position.Y % 10 / 10f) % 1f;

                    var starColor1 = Color.Lerp(colorStart, colorEnd, colorLerp1 * (Main.GameUpdateCount / 60f));
                    var starColor2 = Color.Lerp(colorStart, colorEnd, colorLerp2 * (Main.GameUpdateCount / 60f));
                    var burstColor = Color.Lerp(starColor1, starColor2, 0.5f * (Main.GameUpdateCount / 60f));

                    CreateStardustBurst(collisionPoint, (star1.scale + star2.scale) * 0.5f, burstColor);

                    if (starVelocities.ContainsKey(star1) && starVelocities.ContainsKey(star2))
                    {
                        var pushDir1 = star1.position - star2.position;
                        if (pushDir1 != Vector2.Zero) pushDir1.Normalize();
                        var pushDir2 = -pushDir1;

                        starVelocities[star1] += pushDir1 * 1.1f;
                        starVelocities[star2] += pushDir2 * 1.1f;
                    }
                }
            }
        }
    }

    private static void ProcessAndDrawStars(SpriteBatch spriteBatch, Texture2D glowTexture, Texture2D starTexture, Color colorStart, Color colorEnd)
    {
        for (var i = menuStars.Count - 1; i >= 0; i--)
        {
            var star = menuStars[i];
            star.Update();

            if (!star.falling)
            {
                star.position += starVelocities[star];
                starVelocities[star] = Vector2.Lerp(starVelocities[star], new Vector2(-spaceDriftSpeed * (0.5f + star.scale), 0f), 0.01f);
            }

            var brightness = 0.5f + star.twinkle * 0.5f;
            var colorLerp = (star.type / 4f + (int)star.position.X * (int)star.position.Y % 10 / 10f) % 1f;
            var baseColor = Color.Lerp(colorStart, colorEnd, colorLerp);
            var alpha = star.fadeIn > 0f ? 1f - star.fadeIn : 1f;
            var starColor = baseColor * brightness * alpha;
            var bloomScale = star.scale * (0.8f + star.twinkle * 0.4f);

            spriteBatch.Draw(glowTexture, star.position, null, starColor * 0.6f, 0f, new Vector2(glowTexture.Width / 2, glowTexture.Height / 2), bloomScale * 1.1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(starTexture, star.position, null, starColor, star.rotation, new Vector2(starTexture.Width / 2, starTexture.Height / 2), bloomScale * 0.5f, SpriteEffects.None, 0f);

            if (star.hidden || star.position.Y > Main.screenHeight + 100 || star.position.Y < -100 || star.position.X < -50)
            {
                menuStars.RemoveAt(i);
                if (starVelocities.ContainsKey(star)) starVelocities.Remove(star);
                CreateNewStar();
            }
        }

        if (Main.rand.NextBool(50))
        {
            var starIndex = Main.rand.Next(menuStars.Count);
            menuStars[starIndex].Fall();

            if (starVelocities.ContainsKey(menuStars[starIndex]))
                starVelocities.Remove(menuStars[starIndex]);

            var star = menuStars[starIndex];
            star.rotationSpeed = 0.1f;
            star.rotation = 0.01f;
            star.fallSpeed.Y = Main.rand.Next(100, 201) * 0.001f;
            star.fallSpeed.X = Main.rand.Next(-100, 101) * 0.001f;
        }
    }

    private static bool TryCreateEasterEgg(Vector2 position)
    {
        if (easterEggObjects.Count >= 3) return false;
        if (Main.rand.NextFloat() > EASTER_EGG_CHANCE && position.X < 0) return false;

        var velocity = new Vector2(-EASTER_EGG_DRIFT_SPEED * Main.rand.NextFloat(0.9f, 1.3f), Main.rand.NextFloat(-0.01f, 0.01f));
        var scale = Main.rand.NextFloat(0.3f, 0.7f);
        easterEggObjects.Add(new EasterEggObject(position, velocity, scale));
        return true;
    }

    private static void CreateNewStar(bool initialSetup = false)
    {
        if (!initialSetup && Main.rand.NextFloat() < EASTER_EGG_CHANCE)
        {
            if (TryCreateEasterEgg(new Vector2(Main.screenWidth + 20, Main.rand.Next(100, Main.screenHeight - 100))))
                return;
        }

        var star = new Star
        {
            position = new Vector2(Main.rand.Next(0, Main.screenWidth), Main.rand.Next(0, Main.screenHeight)),
            rotation = Main.rand.NextFloat(0, MathHelper.TwoPi),
            scale = Main.rand.NextFloat(0.2f, 0.72f)
        };

        if (Main.rand.NextBool(20)) star.scale *= 0.8f;

        star.type = Main.rand.Next(0, 4);
        star.twinkle = Main.rand.NextFloat(0.6f, 1f);
        star.twinkleSpeed = Main.rand.NextFloat(0.0005f, 0.004f);

        if (Main.rand.NextBool(5)) star.twinkleSpeed *= 2f;
        if (Main.rand.NextBool()) star.twinkleSpeed *= -1f;

        star.rotationSpeed = Main.rand.NextFloat(0.000001f, 0.00001f);
        if (Main.rand.NextBool()) star.rotationSpeed *= -0.05f;

        star.fadeIn = 0.5f;

        starVelocities[star] = new Vector2(-spaceDriftSpeed * (0.5f + star.scale), 0f);
        menuStars.Add(star);
    }

    private static void CreateStardustBurst(Vector2 position, float scale, Color color)
    {
        var dustCount = Main.rand.Next(8, 16);
        for (var i = 0; i < dustCount; i++)
        {
            var dustVelocity = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
            var dust = Dust.NewDustPerfect(position, DustID.AncientLight, dustVelocity, 0, color, scale * Main.rand.NextFloat(1f, 1.7f));
            dust.noGravity = true;
            dust.fadeIn = 0.1f;
            dust.noLight = false;
        }
    }
}

