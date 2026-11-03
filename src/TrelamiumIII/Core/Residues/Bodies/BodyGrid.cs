namespace TrelamiumIII.Core.Residues.Bodies;

/// <summary>How a residue moves over a body. See <see cref="BodyGrid" />.</summary>
/// <param name="Rise">How fast it climbs to the cell above, in amount per tick. Zero never climbs.</param>
/// <param name="Fall">How fast it spreads to the cell below. Zero never does.</param>
/// <param name="Creep">How fast it spreads to the cells to each side. Zero never does.</param>
/// <param name="Sag">
///     How much of where it landed really moves down each tick, rather than spreading from there. What gets to the bottom row
///     drips off. Liquids run.
/// </param>
/// <param name="Decay">How fast where it landed wears away, in amount per tick, when nothing is keeping it going.</param>
/// <param name="Reach">How much of the body it spreads over by itself, 0..1. A debuff or an affliction can take it further.</param>
public readonly record struct BodyFlow(float Rise = 0.03f, float Fall = 0.03f, float Creep = 0.03f, float Sag = 0f, float Decay = 0.002f, float Reach = 0.3f);

/// <summary>What comes off a coat: nodes that live in the body's slot, drawn by the same canvas and shader as the coat itself.</summary>
/// <remarks>
///     They're stamped into the coat's canvas and opened up in its mask, so they run together with it and with each other. It
///     makes a body look engulfed rather than painted, and costs no more than a few more stamps.
/// </remarks>
/// <param name="Rate">Nodes per tick off a person-sized body with it all over. Scales with how big the body is and how much it covers.</param>
/// <param name="Radius">Pixels at birth on a person-sized body. Grows more slowly than the body does.</param>
/// <param name="Life">Ticks each lasts, at most.</param>
/// <param name="Launch">Velocity at birth, in pixels per tick, with up negative.</param>
/// <param name="Scatter">How far each one's launch can be off that, either way.</param>
/// <param name="Pull">Added to its velocity each tick. Negative y floats, positive falls.</param>
/// <param name="Drag">What's left of its velocity after each tick.</param>
/// <param name="Wind">Pixels per tick per unit of the world's wind.</param>
/// <param name="Cling">How much it moves along with the body, 0..1. Lower trails further behind when it runs.</param>
public readonly record struct BodyShed(float Rate, float Radius, float Life, Vector2 Launch, float Scatter = 0.3f, Vector2 Pull = default, float Drag = 0.98f, float Wind = 0f, float Cling = 0.5f);

/// <summary>One thing come off a coat, relative to the middle of the body.</summary>
internal struct BodyNode
{
    public Vector2 Offset;

    public Vector2 Velocity;

    public float Life;

    public float MaxLife;

    public float Radius;

    /// <summary>How much of the residue it's made of at birth, 0..1.</summary>
    public float Amount;

    public readonly float Fade => MathHelper.Clamp(Life / MaxLife, 0f, 1f);

    /// <summary>Radius as it is now. A node shrinks as it goes, as well as thinning.</summary>
    public readonly float RadiusNow => Radius * (0.5f + 0.5f * Fade);
}

/// <summary>
///     Where one residue is on one body: a small grid of how much of it each part of the body has, the body's answer to tiles.
/// </summary>
/// <remarks>
///     <para>
///         The grid is stretched over the body rather than sized in pixels, so a slime and a boss cost the same. It belongs to
///         the body, so a coat stays on the same side as it turns, and which way up it's drawn is up to the canvas.
///     </para>
///     <para>
///         Two layers. Sources are where it actually landed, and only those are ever laid. Every other cell reaches for what
///         its neighbours have, less a step for the distance, so a coat is strongest where it started and thins out to nothing
///         at its leading edge. That thinning is what makes fire solid at its heart and break into tongues at the tips. How far
///         it can get is how big the step is, so asking for more of the body is what makes it spread further.
///     </para>
///     <para>
///         Nothing here is random, so every machine that hears the same touches draws the same coat.
///     </para>
/// </remarks>
internal sealed class BodyGrid
{
    public const int COLUMNS = 8;

    public const int ROWS = 10;

    /// <summary>Share of a bottom cell's sag that drips off the body rather than staying to pool at the feet.</summary>
    private const float DRIP_SHARE = 0.25f;

    /// <summary>How fast a cell gives up what its neighbours no longer reach it with.</summary>
    private const float THINNING_PER_TICK = 0.02f;

    /// <summary>More of a residue than this counts as being on that part of the body.</summary>
    private const float COVERS = 0.04f;

    private const float GONE_PER_TICK = 0.001f;

    private readonly float[] sources = new float[COLUMNS * ROWS];

    private float[] cells = new float[COLUMNS * ROWS];

    private float[] next = new float[COLUMNS * ROWS];

    /// <summary>How much of the body it's on, 0..1.</summary>
    public float Coverage { get; private set; }

    /// <summary>Whether it's anywhere at all, and has anywhere left to come from.</summary>
    public bool IsEmpty { get; private set; } = true;

    /// <summary>What's at a cell. Past the edge it's what's at the edge, so a coat runs on past the grid and the silhouette cuts it.</summary>
    public float At(int x, int y) => cells[Math.Clamp(y, 0, ROWS - 1) * COLUMNS + Math.Clamp(x, 0, COLUMNS - 1)];

    /// <summary>
    ///     Starts a coat off for something that says how much but not where: along the feet if it can climb from there, or
    ///     from the top if it only runs down.
    /// </summary>
    public void Seed(in BodyFlow flow)
    {
        int row = flow.Rise > 0f ? ROWS - 1 : 0;

        for (int x = 0; x < COLUMNS; x++)
            sources[row * COLUMNS + x] = 1f;

        IsEmpty = false;
    }

    /// <summary>Lays some on the body around a point.</summary>
    /// <param name="u">Across the body, 0..1.</param>
    /// <param name="v">Down the body, 0..1 from the top of the sprite to its feet.</param>
    /// <param name="radius">As a share of the body's height.</param>
    /// <param name="amount">What the middle of it gets, 0..1. The rest thins out from there.</param>
    public void Touch(float u, float v, float radius, float amount)
    {
        // Cells are about as wide as tall on anything shaped like a person, and it needn't be exact.
        float reach = MathF.Max(radius * ROWS, 0.75f);

        for (int y = 0; y < ROWS; y++)
        {
            for (int x = 0; x < COLUMNS; x++)
            {
                float dx = x + 0.5f - u * COLUMNS;
                float dy = y + 0.5f - v * ROWS;
                float falloff = 1f - MathF.Sqrt(dx * dx + dy * dy) / reach;

                if (falloff <= 0f)
                    continue;

                int i = y * COLUMNS + x;

                sources[i] = MathF.Min(sources[i] + amount * falloff, 1f);
            }
        }

        IsEmpty = false;
    }

    /// <summary>Moves everything on by a tick.</summary>
    /// <param name="budget">How much of the body it may reach, 0..1.</param>
    /// <param name="held">Whether something is keeping it going, so that where it landed doesn't wear away.</param>
    public void Step(in BodyFlow flow, float budget, bool held)
    {
        UpdateSources(in flow, held);

        // Enough to run out of strength the right number of rows from where it started.
        float step = 1f / (budget * ROWS + 1f);
        float fastest = MathF.Max(MathF.Max(flow.Rise, flow.Fall), MathF.Max(flow.Creep, 0.001f));

        // The slower a way is, the shorter it goes: fire hardly creeps down at all.
        float riseStep = flow.Rise > 0f ? step * fastest / flow.Rise : float.MaxValue;
        float fallStep = flow.Fall > 0f ? step * fastest / flow.Fall : float.MaxValue;
        float creepStep = flow.Creep > 0f ? step * fastest / flow.Creep : float.MaxValue;

        int covered = 0;
        bool anywhere = false;

        for (int y = 0; y < ROWS; y++)
        {
            for (int x = 0; x < COLUMNS; x++)
            {
                int i = y * COLUMNS + x;
                float here = cells[i];
                float target = sources[i];
                float speed = fastest;

                // The edges have nothing past them, rather than more of the same, or a coat would feed on itself.
                if (y + 1 < ROWS && cells[i + COLUMNS] - riseStep > target)
                {
                    target = cells[i + COLUMNS] - riseStep;
                    speed = flow.Rise;
                }

                if (y > 0 && cells[i - COLUMNS] - fallStep > target)
                {
                    target = cells[i - COLUMNS] - fallStep;
                    speed = flow.Fall;
                }

                if (x > 0 && cells[i - 1] - creepStep > target)
                {
                    target = cells[i - 1] - creepStep;
                    speed = flow.Creep;
                }

                if (x + 1 < COLUMNS && cells[i + 1] - creepStep > target)
                {
                    target = cells[i + 1] - creepStep;
                    speed = flow.Creep;
                }

                float value = here < target ? MathF.Min(here + speed, target) : MathF.Max(here - THINNING_PER_TICK, target);

                next[i] = value;

                if (value > COVERS)
                    covered++;

                anywhere |= value > 0f || sources[i] > 0f;
            }
        }

        (cells, next) = (next, cells);

        Coverage = covered / (float)cells.Length;
        IsEmpty = !anywhere;
    }

    private void UpdateSources(in BodyFlow flow, bool held)
    {
        // From the bottom up, so nothing moves more than a row in a tick.
        for (int y = ROWS - 1; y >= 0; y--)
        {
            for (int x = 0; x < COLUMNS; x++)
            {
                int i = y * COLUMNS + x;
                float source = sources[i];

                if (source <= 0f)
                    continue;

                // Something holding it up holds it in place too. It runs off once that stops.
                if (flow.Sag > 0f && !held)
                {
                    float moving = source * flow.Sag;

                    source -= moving;

                    if (y + 1 < ROWS)
                        sources[i + COLUMNS] = MathF.Min(sources[i + COLUMNS] + moving, 1f);
                    else
                        source += moving * (1f - DRIP_SHARE);
                }

                // A share of what's there, so a thin trail wears away no faster than the blob it came from, and then the
                // last of it goes.
                sources[i] = held ? source : source * (1f - flow.Decay) - GONE_PER_TICK is > 0f and var left ? left : 0f;
            }
        }
    }
}
