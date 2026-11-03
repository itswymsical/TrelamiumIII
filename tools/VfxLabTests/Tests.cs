using TrelamiumIII.Content.DevTools.VfxLab.Bench;
using TrelamiumIII.Content.DevTools.VfxLab.Export;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;

int failures = 0;
int passes = 0;

void Check(string name, object? actual, object? expected)
{
    bool ok = Equals(actual?.ToString(), expected?.ToString());

    if (ok)
    {
        passes++;
        return;
    }

    failures++;
    Console.WriteLine($"  [FAIL] {name}\n         got      <{actual}>\n         expected <{expected}>");
}

void True(string name, bool ok, string detail = "") => Check(name, ok ? "yes" : $"no {detail}".TrimEnd(), "yes");

void Section(string name) => Console.WriteLine($"-- {name} --");

string Throws(Action action)
{
    try
    {
        action();
        return "nothing";
    }
    catch (Exception exception)
    {
        return exception.Message;
    }
}

string root = AppContext.BaseDirectory;

while (!File.Exists(Path.Combine(root, "TrelamiumIII.sln")))
    root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("Run this from inside the repository.");

string shaders = Path.Combine(root, "src", "TrelamiumIII", "Assets", "Shaders");
string tests = Path.Combine(root, "tools", "VfxLabTests");
string scratch = Path.Combine(Path.GetTempPath(), "TrelamiumVfxLabTests", Guid.NewGuid().ToString("N"));
bool updateGolden = args.Contains("--update-golden");

Directory.CreateDirectory(scratch);

StageLibrary library = StageLibrary.FromFolder(shaders);
HostDefinition field = library.Hosts["Field"];
VfxRecipe iceMist = RecipeFile.Load(Path.Combine(tests, "Recipes"), "IceMist");

VfxRecipe Stack(params RecipeStage[] stages) => new() { Name = "Test", Surface = "Field", Stages = [.. stages] };
RecipeStage Stage(string id, string type) => new() { Id = id, Type = type };

// ---- the stage format ----

Section("the library as shipped");
Check("every stage file parses", string.Join("; ", library.Problems), "");
True("has the field host", library.Hosts.ContainsKey("Field"));
True("has the shared code", library.Common.Contains("struct Px"));

foreach (string stage in new[] { "FlowAdvect", "DomainWarp", "NoiseLayer", "Threshold", "DepthShade", "Palette", "LightFloor", "Rim" })
    True($"has {stage}", library.Stages.ContainsKey(stage));

Section("parsing a stage");
const string GoodStage = """
    // A leading comment is fine.
    //@stage Demo phase=Shade needs=heat,light
    //@param Amount float 0.5 range=-1..2.5
    //@param Drift float2 0.1,-0.2
    //@param Tint color 1,0.5,0
    //@param Steps int 3 range=1..6
    //@param Noise noise Perlin

    void $Stage(inout Px px)
    {
        px.shade = $Amount;
    }
    """;

StageDefinition demo = StageParser.ParseStage("Demo.stage.fxh", GoodStage);
Check("name", demo.Name, "Demo");
Check("phase", demo.Phase, StagePhase.Shade);
True("needs", demo.Needs.SetEquals(["heat", "light"]));
Check("param count", demo.Params.Count, 5);
Check("float default", demo.Params[0].Default[0], 0.5f);
Check("range low", demo.Params[0].Min, -1f);
Check("range high", demo.Params[0].Max, 2.5f);
Check("float2 default", demo.Params[1].Default[1], -0.2f);
Check("kind of a colour", demo.Params[2].Kind, ParamKind.Color);
Check("kind of an int", demo.Params[3].Kind, ParamKind.Int);
Check("noise default", demo.Params[4].DefaultTexture, "Perlin");
Check("body starts on its real line", demo.BodyLine, 9);
Check("CRLF parses the same", StageParser.ParseStage("Demo.stage.fxh", GoodStage.Replace("\n", "\r\n")).Body, demo.Body);

Section("stage errors name the file and line");

string BadStage(string text) => Throws(() => StageParser.ParseStage("Bad.stage.fxh", text));

const string Body = "void $Stage(inout Px px) { }";

Check("no directive", BadStage(Body), "Bad.stage.fxh(1): no //@stage directive.");
Check("no phase", BadStage($"//@stage A\n{Body}"), "Bad.stage.fxh(1): //@stage needs a phase=.");
Check("bad phase", BadStage($"//@stage A phase=Nope\n{Body}"), "Bad.stage.fxh(1): unknown phase 'Nope'.");
Check("numeric phase is still bad", BadStage($"//@stage A phase=99\n{Body}"), "Bad.stage.fxh(1): unknown phase '99'.");
Check("bad type", BadStage($"//@stage A phase=Cut\n//@param B vector 1\n{Body}"), "Bad.stage.fxh(2): unknown type 'vector'.");
Check("bad number", BadStage($"//@stage A phase=Cut\n//@param B float x\n{Body}"), "Bad.stage.fxh(2): 'x' isn't a number.");
Check("wrong count", BadStage($"//@stage A phase=Cut\n//@param B float2 1\n{Body}"), "Bad.stage.fxh(2): '1' should be 2 numbers.");
Check("bad range", BadStage($"//@stage A phase=Cut\n//@param B float 1 range=5\n{Body}"), "Bad.stage.fxh(2): range '5' isn't low..high.");
Check("duplicate param", BadStage($"//@stage A phase=Cut\n//@param B float 1\n//@param B float 1\n{Body}"), "Bad.stage.fxh(3): the parameter B is declared twice.");
Check("param called Stage", BadStage($"//@stage A phase=Cut\n//@param Stage float 1\n{Body}"), "Bad.stage.fxh(2): Stage is the name of the stage's function.");
Check("directive after code", BadStage($"//@stage A phase=Cut\n{Body}\n//@param B float 1"), "Bad.stage.fxh(3): directives have to come before the code.");
Check("unknown directive", BadStage($"//@stage A phase=Cut\n//@nope\n{Body}"), "Bad.stage.fxh(2): unknown directive '//@nope'.");
Check("typo in a token", BadStage("//@stage A phase=Cut\n//@param Amount float 1\n\nvoid $Stage(inout Px px)\n{\n    px.shade = $Amout;\n}"), "Bad.stage.fxh(6): $Amout isn't a parameter of this stage.");
Check("no stage function", BadStage("//@stage A phase=Cut\nvoid Other() { }"), "Bad.stage.fxh(2): needs a function named $Stage.");

Section("one bad file doesn't take down the library");
StageLibrary partial = StageLibrary.From([
    ("Lab/Common.fxh", "struct Px { float shade; };"),
    ("Lab/Stages/Good.stage.fxh", $"//@stage Good phase=Cut\n{Body}"),
    ("Lab/Stages/Bad.stage.fxh", "//@stage Bad phase=Nope\n" + Body),
    ("Lab/Stages/Twin.stage.fxh", $"//@stage Good phase=Cut\n{Body}"),
    ("Lab/Notes.fxh", "ignored"),
]);
True("the good stage loaded", partial.Stages.ContainsKey("Good"));
True("the bad stage didn't", !partial.Stages.ContainsKey("Bad"));
Check("the bad file is reported", partial.Problems.Count(problem => problem.StartsWith("Lab/Stages/Bad.stage.fxh(1)")), 1);
Check("the duplicate is reported", partial.Problems.Count(problem => problem.Contains("already defines the stage Good")), 1);
True("a library with no shared code says so", StageLibrary.From([]).Problems.Any(problem => problem.Contains("Common.fxh")));

Section("compatible stages");
HostDefinition small = StageParser.ParseHost("Small.host.fxh", "//@host Small\n//@provides density\n//@stages\n//@calls");
True("Threshold fits a host with density", library.CompatibleStages(small).Any(stage => stage.Name == "Threshold"));
True("NoiseLayer needs nothing", library.CompatibleStages(small).Any(stage => stage.Name == "NoiseLayer"));
True("Rim doesn't fit a host with no neighbours", library.CompatibleStages(small).All(stage => stage.Name != "Rim"));
Check("every stage fits Field", library.CompatibleStages(field).Count(), library.Stages.Count);

Section("host errors");
Check("no markers", Throws(() => StageParser.ParseHost("H.host.fxh", "//@host H")), "H.host.fxh(1): needs //@stages and then //@calls.");
Check("markers backwards", Throws(() => StageParser.ParseHost("H.host.fxh", "//@host H\n//@calls\n//@stages")), "H.host.fxh(1): needs //@stages and then //@calls.");
Check("no name", Throws(() => StageParser.ParseHost("H.host.fxh", "//@stages\n//@calls")), "H.host.fxh(1): no //@host directive.");
Check("field host counts its samplers", field.SamplerCount, 2);

// ---- recipes ----

Section("recipe round trip");
string saved = RecipeFile.Serialize(iceMist);
VfxRecipe reread = RecipeFile.Parse(saved);
Check("name survives", reread.Name, "IceMist");
Check("stage count survives", reread.Stages.Count, iceMist.Stages.Count);
Check("a second save is identical", RecipeFile.Serialize(reread), saved);
True("colours stay on one line", saved.Contains("[0.36, 0.52, 0.68]"), saved);
True("enabled isn't written when it's the default", !saved.Contains("enabled"));
True("empty layers aren't written", !saved.Contains("layers"));

Section("recipe values");
RecipeStage set = Stage("a", "Palette").Set("Alpha", 0.36f).Set("Rgb", 0.36f, 0.52f, 0.68f).Set("Count", 4).Set("Name", "Perlin");
string written = RecipeFile.Serialize(Stack(set));
True("a float saves as its shortest form", written.Contains("\"Alpha\": 0.36,"), written);
True("float arrays do too", written.Contains("[0.36, 0.52, 0.68]"), written);
True("an int stays an int", written.Contains("\"Count\": 4"), written);

Section("what this version doesn't read is kept");
const string FromTheFuture = """
    {
      "format": 1,
      "name": "SapMist",
      "surface": "Field",
      "stages": [ { "id": "a", "type": "Threshold", "future": true } ],
      "physics": { "drag": 0.96 },
      "modules": [ { "type": "WindForce", "Pull": 0.02 } ],
      "links": { "deposit": { "residue": "SapCrust" } }
    }
    """;
string kept = RecipeFile.Serialize(RecipeFile.Parse(FromTheFuture));
True("physics", kept.Contains("\"drag\": 0.96"), kept);
True("modules", kept.Contains("WindForce"), kept);
True("links", kept.Contains("SapCrust"), kept);
True("unknown stage values", kept.Contains("\"future\": true"), kept);

Section("recipes that won't load");
True("newer format", Throws(() => RecipeFile.Parse("""{ "format": 2, "name": "A" }""")).Contains("format 2"));
True("bad name", Throws(() => RecipeFile.Parse("""{ "format": 1, "name": "a b" }""")).Contains("isn't a valid recipe name"));
True("empty name", Throws(() => RecipeFile.Parse("""{ "format": 1 }""")).Contains("isn't a valid recipe name"));
True("not json", Throws(() => RecipeFile.Parse("{ nope")) != "nothing");
True("missing file text", Throws(() => RecipeFile.Parse("")).Contains("empty"));

Section("recipes reference each other by name");
string folder = Path.Combine(scratch, "recipes");
RecipeFile.Save(folder, new VfxRecipe { Name = "Stars" });
var scene = new VfxRecipe { Name = "Title", Layers = ["Stars", "Nebula"] };
RecipeFile.Save(folder, scene);
Check("names lists the folder", string.Join(",", RecipeFile.Names(folder)), "Stars,Title");
Check("load finds it by name", RecipeFile.Load(folder, "Title").Layers.Count, 2);
Check("reports the missing layer", string.Join(",", RecipeFile.MissingLayers(folder, scene)), "Nebula");
True("layers are written when there are some", RecipeFile.Serialize(scene).Contains("\"layers\""));
Check("a folder that doesn't exist has no recipes", RecipeFile.Names(Path.Combine(scratch, "nope")).Count(), 0);

// ---- generation ----

Section("generating a shader");
GeneratedShader generated = ShaderGenerator.Generate(iceMist, library);
string source = generated.Source ?? "";
True("succeeds", generated.Succeeded, string.Join("; ", generated.Problems));
Check("no problems", string.Join("; ", generated.Problems), "");
True("names the recipe", source.StartsWith("// Generated by the VFX Lab from the recipe IceMist."));
True("no tokens left", !source.Contains('$'));
True("no directives left", !source.Contains("//@"));
True("uses the id for uniforms", source.Contains("float cut_Level;"));
True("calls in order", source.IndexOf("flow_Stage(px);") < source.IndexOf("noise_Stage(px);") && source.IndexOf("noise_Stage(px);") < source.IndexOf("cut_Stage(px);"));
True("errors map to the stage file", source.Contains("#line 7 \"Lab/Stages/Threshold.stage.fxh\""), source);
True("errors map to the host", source.Contains("Lab/Hosts/Field.host.fxh"));
True("errors map to the shared code", source.Contains("#line 1 \"Lab/Common.fxh\""));
True("int params are literals", source.Contains("LabFbm(noise_Noise, p, 4)"), source);
True("has one technique", source.Split("technique ").Length == 2);
Check("counts samplers", generated.SamplerCount, 3);

Section("values");
UniformValue Uniform(GeneratedShader shader, string name) => shader.Uniforms.Single(uniform => uniform.Name == name);
Check("a set value", Uniform(generated, "cut_Level").Values[0], 0.14f);
Check("a colour", Uniform(generated, "palette_Body").Values[1], 0.8f);
Check("a default", Uniform(generated, "noise_Pivot").Values[0], 0.5f);
Check("a texture", Uniform(generated, "noise_NoiseTex").Texture, "LiquidNoise");
True("an int isn't a uniform", generated.Uniforms.All(uniform => uniform.Name != "noise_Octaves"));

GeneratedShader hex = ShaderGenerator.Generate(Stack(Stage("a", "Palette").Set("Body", "#ff8000")), library);
Check("hex red", Uniform(hex, "a_Body").Values[0], 1f);
Check("hex green", Uniform(hex, "a_Body").Values[1], 128 / 255f);
Check("hex blue", Uniform(hex, "a_Body").Values[2], 0f);

Section("values never reach the source");
VfxRecipe retuned = RecipeFile.Parse(saved);
retuned.Stages.Single(stage => stage.Id == "cut").Set("Level", 0.5f);
retuned.Stages.Single(stage => stage.Id == "noise").Set("Noise", "Perlin");
GeneratedShader retunedShader = ShaderGenerator.Generate(retuned, library);
Check("a retuned recipe has the same source", retunedShader.Source, source);
Check("with the new value", Uniform(retunedShader, "cut_Level").Values[0], 0.5f);
retuned.Stages.Single(stage => stage.Id == "noise").Set("Octaves", 2);
True("an int does change the source", ShaderGenerator.Generate(retuned, library).Source != source);

Section("phases and instances");
GeneratedShader layered = ShaderGenerator.Generate(Stack(
    Stage("rim", "Rim"),
    Stage("b", "NoiseLayer"),
    Stage("cut", "Threshold"),
    Stage("a", "NoiseLayer"),
    Stage("warp", "DomainWarp")), library);
string layeredSource = layered.Source ?? "";
True("two of one stage coexist", layeredSource.Contains("float a_Scale;") && layeredSource.Contains("float b_Scale;"), string.Join("; ", layered.Problems));
int Call(string id) => layeredSource.IndexOf($"    {id}_Stage(px);");
True("phases beat recipe order", Call("warp") < Call("b") && Call("a") < Call("cut") && Call("cut") < Call("rim"));
True("recipe order holds within a phase", Call("b") < Call("a"));

RecipeStage off = Stage("off", "NoiseLayer");
off.Enabled = false;
GeneratedShader skipped = ShaderGenerator.Generate(Stack(off, Stage("on", "Threshold")), library);
True("a disabled stage isn't generated", skipped.Source?.Contains("off_") == false, skipped.Source ?? "");
True("a disabled stage is kept in the recipe", RecipeFile.Serialize(Stack(off)).Contains("\"enabled\": false"));

Section("recipe problems");

string Problem(VfxRecipe recipe) => string.Join("; ", ShaderGenerator.Generate(recipe, library).Problems);

True("unknown surface stops it", !ShaderGenerator.Generate(new VfxRecipe { Name = "T", Surface = "Nope" }, library).Succeeded);
Check("unknown surface", Problem(new VfxRecipe { Name = "T", Surface = "Nope" }), "error in T: there's no surface called 'Nope'.");
Check("unknown stage", Problem(Stack(Stage("a", "Nope"))), "error in a: there's no stage called 'Nope'.");
Check("duplicate id", Problem(Stack(Stage("a", "Threshold"), Stage("a", "Rim"))), "error in a: another stage in this recipe has that id.");
Check("bad id", Problem(Stack(Stage("a_b", "Threshold"))), "error in a_b: an id has to be letters and digits, starting with a letter.");
Check("empty id", Problem(Stack(Stage("", "Threshold"))), "error in : an id has to be letters and digits, starting with a letter.");
True("a recipe with an error generates nothing", !ShaderGenerator.Generate(Stack(Stage("a", "Nope")), library).Succeeded);

StageLibrary narrow = StageLibrary.From([
    ("Lab/Common.fxh", library.Common),
    ("Lab/Hosts/Small.host.fxh", "//@host Small\n//@stages\n//@calls"),
    ("Lab/Stages/Rim.stage.fxh", "//@stage Rim phase=Edge needs=neighbours\n" + Body),
]);
Check("a stage the surface can't run",
    string.Join("; ", ShaderGenerator.Generate(new VfxRecipe { Name = "T", Surface = "Small", Stages = [Stage("a", "Rim")] }, narrow).Problems),
    "error in a: Rim needs neighbours, which the Small surface doesn't have.");

Check("unknown value is a warning", Problem(Stack(Stage("a", "Threshold").Set("Nope", 1f))), "warning in a: Threshold has no parameter called 'Nope', so it was ignored.");
GeneratedShader wrong = ShaderGenerator.Generate(Stack(Stage("a", "Threshold").Set("Level", "high")), library);
True("a wrong value still generates", wrong.Succeeded);
Check("wrong value is a warning", wrong.Problems[0].ToString(), "warning in a: Level should be a number, so the default was used.");
Check("wrong value takes the default", Uniform(wrong, "a_Level").Values[0], 0.22f);
Check("wrong length", Problem(Stack(Stage("a", "NoiseLayer").Set("Drift", 1f, 2f, 3f))), "warning in a: Drift should be two numbers, so the default was used.");
Check("bad hex", Problem(Stack(Stage("a", "Palette").Set("Body", "#nope"))), "warning in a: Body should be three numbers or #rrggbb, so the default was used.");
Check("wrong int", Problem(Stack(Stage("a", "NoiseLayer").Set("Octaves", 1.5f))), "warning in a: Octaves should be a whole number, so the default was used.");
Check("wrong texture", Problem(Stack(Stage("a", "NoiseLayer").Set("Noise", 1f))), "warning in a: Noise should be a texture name, so the default was used.");

GeneratedShader clamped = ShaderGenerator.Generate(Stack(Stage("a", "NoiseLayer").Set("Octaves", 99)), library);
True("an octave count is held to its range", clamped.Source?.Contains("LabFbm(a_Noise, p, 6)") == true, clamped.Source ?? "");
Check("and says so", clamped.Problems[0].ToString(), "warning in a: Octaves of 99 is outside 1..6, so it's 6.");

List<RecipeStage> tooMany = [];

for (int i = 0; i < 15; i++)
    tooMany.Add(Stage($"n{i}", "NoiseLayer"));

GeneratedShader greedy = ShaderGenerator.Generate(Stack([.. tooMany]), library);
True("too many textures stops it", !greedy.Succeeded);
Check("and says so", greedy.Problems[^1].ToString(), "error in Test: this stack needs 17 textures and a pass can bind 16.");
tooMany.RemoveAt(0);
True("sixteen is allowed", ShaderGenerator.Generate(Stack([.. tooMany]), library).Succeeded);

Section("CRLF stage files generate the same source");
StageLibrary crlf = StageLibrary.From(
    Directory.EnumerateFiles(Path.Combine(shaders, StageLibrary.Folder), "*.fxh", SearchOption.AllDirectories)
        .Select(path => (Path.GetRelativePath(shaders, path).Replace('\\', '/'), File.ReadAllText(path).Replace("\r\n", "\n").Replace("\n", "\r\n"))));
Check("same source", ShaderGenerator.Generate(iceMist, crlf).Source, source);

Section("golden");
string goldenPath = Path.Combine(tests, "Golden", "IceMist.fx");

if (updateGolden)
{
    Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
    File.WriteAllText(goldenPath, source);
    Console.WriteLine("  (golden updated)");
}

True("matches the golden file", File.Exists(goldenPath) && StageParser.Normalize(File.ReadAllText(goldenPath)) == source, "(run with --update-golden after a deliberate change, and read the diff)");

// ---- the build queue ----

Section("build queue");

var compiles = new List<string>();
var swaps = new List<string>();
var pendingCompiles = new Queue<TaskCompletionSource<ShaderCompileResult>>();
var failingSwap = false;

var queue = new ShaderBuildQueue(
    compiled =>
    {
        compiles.Add(compiled);
        var completion = new TaskCompletionSource<ShaderCompileResult>();
        pendingCompiles.Enqueue(completion);
        return completion.Task;
    },
    code =>
    {
        if (failingSwap)
            throw new InvalidOperationException("bad effect");

        swaps.Add(System.Text.Encoding.UTF8.GetString(code));
    });

void Complete(string text) => pendingCompiles.Dequeue().SetResult(new ShaderCompileResult(System.Text.Encoding.UTF8.GetBytes(text), "", 1d, false));

Check("starts idle", queue.Status, ShaderBuildStatus.Idle);

queue.Submit("A");
Check("submitting marks it compiling", queue.Status, ShaderBuildStatus.Compiling);
Check("nothing starts until the tick", compiles.Count, 0);
queue.Update();
Check("the tick starts it", string.Join(",", compiles), "A");
queue.Update();
Check("waiting doesn't start it twice", compiles.Count, 1);

Complete("A");
Check("nothing swaps off the main thread", swaps.Count, 0);
queue.Update();
Check("the tick swaps it", string.Join(",", swaps), "A");
Check("compiled", queue.Status, ShaderBuildStatus.Compiled);

queue.Submit("A");
queue.Update();
Check("the same source doesn't recompile", compiles.Count, 1);
Check("and stays compiled", queue.Status, ShaderBuildStatus.Compiled);

queue.Submit("B");
queue.Update();
queue.Submit("C");
queue.Submit("D");
queue.Update();
Check("one compile at a time", string.Join(",", compiles), "A,B");
Complete("B");
queue.Update();
Check("a superseded result is dropped", string.Join(",", swaps), "A");
Check("the newest starts next, and the middle one never does", string.Join(",", compiles), "A,B,D");
Check("still compiling", queue.Status, ShaderBuildStatus.Compiling);
Complete("D");
queue.Update();
Check("the newest lands", string.Join(",", swaps), "A,D");

queue.Submit("E");
queue.Update();
queue.Submit("F");
queue.Submit("E");
queue.Update();
Complete("E");
queue.Update();
Check("going back to what's compiling keeps it", string.Join(",", swaps), "A,D,E");
Check("and doesn't compile the one in between", compiles.Contains("F"), false);

queue.Submit("G");
queue.Update();
queue.Submit("E");
queue.Update();
Complete("G");
queue.Update();
Check("undoing to what's on screen skips the swap", string.Join(",", swaps), "A,D,E");
Check("and the compile", compiles.Count(compiled => compiled == "E"), 1);
Check("and ends compiled", queue.Status, ShaderBuildStatus.Compiled);

queue.Submit("H");
queue.Update();
pendingCompiles.Dequeue().SetResult(new ShaderCompileResult(null, "H(1): error", 1d, false));
queue.Update();
Check("a failed compile reports", queue.Status, ShaderBuildStatus.Failed);
Check("with the log", queue.Log, "H(1): error");
Check("and leaves the last good shader", swaps[^1], "E");

queue.Submit("E");
queue.Update();
Check("returning to the good one is free", queue.Status, ShaderBuildStatus.Compiled);

queue.Submit("I");
queue.Update();
pendingCompiles.Dequeue().SetException(new InvalidOperationException("fxc went away"));
queue.Update();
Check("a compile that throws reports", queue.Status, ShaderBuildStatus.Failed);
Check("with the reason", queue.Log, "fxc went away");

failingSwap = true;
queue.Submit("J");
queue.Update();
Complete("J");
queue.Update();
failingSwap = false;
Check("a swap that throws reports", queue.Status, ShaderBuildStatus.Failed);
Check("with the reason", queue.Log, "bad effect");
queue.Submit("J");
queue.Update();
Complete("J");
queue.Update();
Check("and can be retried", queue.Status, ShaderBuildStatus.Compiled);

// ---- editing a recipe's structure ----

Section("a new field");
VfxRecipe fresh = RecipeEdit.NewField("Sap");
Check("takes the name", fresh.Name, "Sap");
GeneratedShader freshShader = ShaderGenerator.Generate(fresh, library);
True("generates with no problems", freshShader.Succeeded && freshShader.Problems.Count == 0, string.Join("; ", freshShader.Problems));

Section("adding and removing stages");
VfxRecipe edited = RecipeEdit.NewField("Sap");
Check("an id comes from the type", RecipeEdit.AddStage(edited, "DomainWarp").Id, "domainWarp");
Check("and is made unique", RecipeEdit.AddStage(edited, "DomainWarp").Id, "domainWarp2");
Check("again", RecipeEdit.AddStage(edited, "DomainWarp").Id, "domainWarp3");
True("what it makes is a legal id", ShaderGenerator.Generate(edited, library).Succeeded, string.Join("; ", ShaderGenerator.Generate(edited, library).Problems));
True("removes by id", RecipeEdit.RemoveStage(edited, "domainWarp2") && edited.Stages.All(stage => stage.Id != "domainWarp2"));
True("nothing to remove", !RecipeEdit.RemoveStage(edited, "nope"));
Check("the id is free again", RecipeEdit.AddStage(edited, "DomainWarp").Id, "domainWarp2");

Section("moving stages");
VfxRecipe moving = Stack(Stage("a", "NoiseLayer"), Stage("cut", "Threshold"), Stage("b", "NoiseLayer"), Stage("c", "NoiseLayer"));
string Ids(VfxRecipe recipe) => string.Join(",", recipe.Stages.Select(stage => stage.Id));
True("moves within its phase, past another phase", RecipeEdit.MoveStage(moving, "a", 1, library));
Check("swaps with the next of its phase", Ids(moving), "b,cut,a,c");
True("and back", RecipeEdit.MoveStage(moving, "a", -1, library));
Check("swaps back", Ids(moving), "a,cut,b,c");
True("already first in its phase", !RecipeEdit.MoveStage(moving, "a", -1, library));
True("already last in its phase", !RecipeEdit.MoveStage(moving, "c", 1, library));
True("alone in its phase", !RecipeEdit.MoveStage(moving, "cut", 1, library));
True("no such stage", !RecipeEdit.MoveStage(moving, "nope", 1, library));
Check("the stack shows run order", string.Join(",", RecipeEdit.InRunOrder(moving, library).Select(stage => stage.Id)), "a,b,c,cut");

Section("undo");
var history = new RecipeHistory();
True("nothing to undo at first", !history.CanUndo && !history.CanRedo && history.Undo() is null && history.Redo() is null);
True("the first state records", history.Push("a"));
True("still nothing to undo", !history.CanUndo);
history.Push("b");
history.Push("c");
True("the same state twice is one state", !history.Push("c"));
Check("undo", history.Undo(), "b");
Check("undo again", history.Undo(), "a");
True("stops at the start", !history.CanUndo);
Check("redo", history.Redo(), "b");
history.Push("d");
True("a new edit drops what was undone", !history.CanRedo);
Check("undo after that", history.Undo(), "b");

for (int i = 0; i < 300; i++)
    history.Push($"s{i}");

int steps = 0;
while (history.Undo() is not null)
    steps++;
Check("keeps a limited number of steps", steps, 199);

Section("physics, modules and links survive a save");
VfxRecipe full = RecipeEdit.NewField("Full");
full.Physics.Kind = RecipePhysics.Liquid;
full.Physics.Drag = 0.9f;
full.Physics.MaxNodes = 123;
full.Modules.Add(new RecipeModule { Type = "WindForce" }.Set("Pull", 0.03f));
full.Links.Deposit = new RecipeDeposit { Residue = "SlimedCondition", Threshold = 0.7f, Interval = 8 };
full.Emitter.Rate = 30f;
string fullText = RecipeFile.Serialize(full);
VfxRecipe fullBack = RecipeFile.Parse(fullText);
Check("kind", fullBack.Physics.Kind, "Liquid");
Check("drag", fullBack.Physics.Drag, 0.9f);
Check("max nodes", fullBack.Physics.MaxNodes, 123);
Check("module type", fullBack.Modules[0].Type, "WindForce");
Check("module value", fullBack.Modules[0].Get("Pull", 0f), 0.03f);
Check("a module's missing value falls back", fullBack.Modules[0].Get("Nope", 7f), 7f);
Check("residue", fullBack.Links.Deposit?.Residue, "SlimedCondition");
Check("threshold", fullBack.Links.Deposit?.Threshold, 0.7f);
Check("interval", fullBack.Links.Deposit?.Interval, 8);
Check("emitter", fullBack.Emitter.Rate, 30f);
Check("saves the same again", RecipeFile.Serialize(fullBack), fullText);
True("physics numbers stay readable", fullText.Contains("\"drag\": 0.9,"), fullText);
True("a recipe with no links doesn't write them", !RecipeFile.Serialize(RecipeEdit.NewField("Plain")).Contains("links"));
Check("an old recipe with no physics gets the defaults", RecipeFile.Parse("""{ "format": 1, "name": "Old" }""").Physics.Drag, 0.984f);

Section("what's in a stage, for the inspector");
ParamDefinition levelParam = library.Stages["Threshold"].Params[0];
Check("the default when unset", ShaderGenerator.NumbersOf(Stage("a", "Threshold"), levelParam)[0], 0.22f);
Check("the value when set", ShaderGenerator.NumbersOf(Stage("a", "Threshold").Set("Level", 0.5f), levelParam)[0], 0.5f);
Check("an int", ShaderGenerator.NumbersOf(Stage("a", "NoiseLayer").Set("Octaves", 3), library.Stages["NoiseLayer"].Params.Single(parameter => parameter.Name == "Octaves"))[0], 3f);
Check("a texture", ShaderGenerator.TextureOf(Stage("a", "NoiseLayer").Set("Noise", "Perlin"), library.Stages["NoiseLayer"].Params.Single(parameter => parameter.Name == "Noise")), "Perlin");

float[] edited2 = ShaderGenerator.NumbersOf(Stage("a", "Threshold"), levelParam);
edited2[0] = 0.9f;
Check("editing what it returns leaves the stage's default alone", ShaderGenerator.NumbersOf(Stage("a", "Threshold"), levelParam)[0], 0.22f);

Section("debug views");
VfxRecipe withWarp = RecipeEdit.NewField("Views");
RecipeEdit.AddStage(withWarp, "DomainWarp");
True("Final is the recipe itself", ReferenceEquals(ShaderViews.Apply(withWarp, ShaderView.Final, library), withWarp));

foreach (ShaderView view in Enum.GetValues<ShaderView>().Where(view => view != ShaderView.Final))
{
    VfxRecipe viewed = ShaderViews.Apply(withWarp, view, library);
    GeneratedShader viewShader = ShaderGenerator.Generate(viewed, library);

    True($"{view} generates", viewShader.Succeeded, string.Join("; ", viewShader.Problems));
    True($"{view} keeps the noise and warp", viewed.Stages.Any(stage => stage.Type == "NoiseLayer") && viewed.Stages.Any(stage => stage.Type == "DomainWarp"));
    True($"{view} drops the cut and the look", viewed.Stages.All(stage => stage.Type is not ("Threshold" or "Palette" or "Rim")));
    Check($"{view} ends on its own stage", viewed.Stages[^1].Type, $"View{view}");
}

Check("the recipe itself isn't touched", withWarp.Stages.Count, 7);

// ---- copying values back out ----

Section("copy as C#");
Check("one number", BenchExport.Literal([0.3f]), "0.3f");
Check("a whole number keeps its suffix", BenchExport.Literal([2f]), "2f");
Check("two", BenchExport.Literal([1f, 0.5f]), "new Vector2(1f, 0.5f)");
Check("three", BenchExport.Literal([0.36f, 0.52f, 0.68f]), "new Vector3(0.36f, 0.52f, 0.68f)");
Check("four", BenchExport.Literal([1f, 2f, 3f, 4f]), "new Vector4(1f, 2f, 3f, 4f)");
Check("a very small number stays valid C#", BenchExport.Literal([1E-05f]), "1E-05f");

Check("a GasLook uses its own property names",
    BenchExport.GasLook([
        new ExportValue("Threshold", [0.3f], null),
        new ExportValue("NoiseDrift", [0.05f, 0.1f], null),
        new ExportValue("ShadowColor", [0.1f, 0.2f, 0.3f], null),
        new ExportValue("uNoiseTex", null, "Perlin"),
    ]),
    "Look with\n{\n    Threshold = 0.3f,\n    Drift = new Vector2(0.05f, 0.1f),\n    Shadow = new Vector3(0.1f, 0.2f, 0.3f),\n    Noise = static () => Assets.Textures.Noise.Perlin.Asset,\n}");

Check("what a look can't hold is left as parameter sets",
    BenchExport.GasLook([new ExportValue("Bands", [8f], null), new ExportValue("uSomething", [1f], null)]),
    "Look with\n{\n    Bands = 8f,\n}\n\n// Not part of the look:\neffect.Parameters[\"uSomething\"]?.SetValue(1f);");

Check("nothing the look holds at all", BenchExport.GasLook([new ExportValue("uSomething", [1f], null)]), "effect.Parameters[\"uSomething\"]?.SetValue(1f);");

Check("a film look's names match its uniforms",
    BenchExport.FilmLook([new ExportValue("NoiseDrift", [0f, 1f], null), new ExportValue("BodyColor", [1f, 1f, 1f], null)]),
    "Look with\n{\n    NoiseDrift = new Vector2(0f, 1f),\n    BodyColor = new Vector3(1f, 1f, 1f),\n}");

Check("a film's noise lives on its condition",
    BenchExport.FilmLook([new ExportValue("Bands", [4f], null), new ExportValue("uNoiseTex", null, "Melt_01")]),
    "Look with\n{\n    Bands = 4f,\n}\n\nprotected override Asset<Texture2D> NoiseTexture => Assets.Textures.Noise.Melt_01.Asset;");

Check("plain parameters",
    BenchExport.Parameters([new ExportValue("uOpacity", [0.5f], null), new ExportValue("uNoise", null, "Perlin")]),
    "effect.Parameters[\"uOpacity\"]?.SetValue(0.5f);\neffect.Parameters[\"uNoise\"]?.SetValue(Assets.Textures.Noise.Perlin.Asset.Value);");

// ---- the real compiler ----

Section("fxc");

if (ShaderCompiler.FxcPath is null)
{
    Console.WriteLine("  (skipped: the SDK's fxc.exe isn't in the NuGet cache)");
}
else
{
    string cache = Path.Combine(scratch, "cache");

    ShaderCompileResult Compile(string text) => ShaderCompiler.CompileAsync(text, cache).GetAwaiter().GetResult();

    foreach (string shader in new[] { "GasFbm", "TileConditionMask", "HighlandsCapture" })
    {
        byte[] committed = File.ReadAllBytes(Path.Combine(shaders, $"{shader}.fxc"));
        ShaderCompileResult compiled = Compile(File.ReadAllText(Path.Combine(shaders, $"{shader}.fx")));

        True($"{shader} compiles byte-identical to the committed .fxc", compiled.Code?.SequenceEqual(committed) == true, compiled.Log);
    }

    ShaderCompileResult ice = Compile(source);
    True("the golden recipe compiles", ice.Code is not null, ice.Log);
    Check("with no warnings", ice.Log, "");

    ShaderCompileResult iceAgain = Compile(source);
    True("the second compile is cached", iceAgain.Cached);
    True("with the same bytes", iceAgain.Code?.SequenceEqual(ice.Code!) == true);

    // Every stage on the host it can run on, alone and with what it reads.
    foreach (StageDefinition stage in library.CompatibleStages(field))
    {
        GeneratedShader alone = ShaderGenerator.Generate(Stack(Stage("only", stage.Name)), library);
        ShaderCompileResult result = Compile(alone.Source ?? "");

        True($"{stage.Name} compiles alone", result.Code is not null, result.Log);
        Check($"{stage.Name} compiles without warnings", result.Log, "");
    }

    GeneratedShader heavy = ShaderGenerator.Generate(Stack(
        Stage("flow", "FlowAdvect"),
        Stage("warp", "DomainWarp"),
        Stage("warp2", "DomainWarp"),
        Stage("a", "NoiseLayer"),
        Stage("b", "NoiseLayer"),
        Stage("c", "NoiseLayer"),
        Stage("cut", "Threshold"),
        Stage("shade", "DepthShade"),
        Stage("palette", "Palette"),
        Stage("light", "LightFloor"),
        Stage("rim", "Rim")), library);
    ShaderCompileResult heavyResult = Compile(heavy.Source ?? "");
    True("a heavy stack compiles", heavyResult.Code is not null, heavyResult.Log);
    Check("without warnings", heavyResult.Log, "");

    // A hand-written shader compiled where it sits, for hot reload.
    foreach (string shader in new[] { "LiquidSurface", "GasFbm" })
    {
        byte[] committed = File.ReadAllBytes(Path.Combine(shaders, $"{shader}.fxc"));
        ShaderCompileResult inPlace = ShaderCompiler.CompileFileAsync(Path.Combine(shaders, $"{shader}.fx"), cache).GetAwaiter().GetResult();

        True($"{shader} compiled in place matches the committed .fxc", inPlace.Code?.SequenceEqual(committed) == true, inPlace.Log);
    }

    Check("compiling in place leaves nothing behind", Directory.EnumerateFiles(cache, "*.fxc").Count(path => Guid.TryParse(Path.GetFileNameWithoutExtension(path), out _)), 0);

    ShaderCompileResult missing = ShaderCompiler.CompileFileAsync(Path.Combine(scratch, "Nope.fx"), cache).GetAwaiter().GetResult();
    True("a file that isn't there is a failed compile, not a crash", missing.Code is null && missing.Log.Length > 0, missing.Log);

    ShaderCompileResult broken = Compile("#line 10 \"Lab/Stages/Warp.stage.fxh\"\nfloat4 LabPass(float2 uv : TEXCOORD0) : COLOR0 { return nope; }\ntechnique L { pass P0 { PixelShader = compile ps_3_0 LabPass(); } }\n");
    True("a broken shader has no code", broken.Code is null);
    True("its error names the stage file", broken.Log.Contains("Lab/Stages/Warp.stage.fxh(10"), broken.Log);
}

// ---- exporting ----

ExportCatalog exportCatalog = new()
{
    Modules = new Dictionary<string, ModuleSchema>
    {
        ["WindForce"] = new("TrelamiumIII.Core.Graphics.Vfx.Modules", [("Pull", 0.02f), ("Scale", 1f)]),
        ["CurlFlow"] = new("TrelamiumIII.Core.Graphics.Vfx.Modules", [("Strength", 0.4f)]),
    },
    Conditions = new Dictionary<string, (string, string)> { ["SlimedCondition"] = ("TrelamiumIII.Core.Tiles.Conditions.Types", "SlimedCondition") },
    TakenNames = new HashSet<string> { "MudField" },
};

VfxRecipe ExportRecipe()
{
    VfxRecipe recipe = RecipeFile.Parse(RecipeFile.Serialize(iceMist));

    recipe.Name = "IceExport";
    recipe.Physics.Kind = RecipePhysics.Liquid;
    recipe.Physics.Density = 0.36f;
    recipe.Physics.MaxNodes = 250;
    recipe.Physics.TemperatureMax = 1500f;
    recipe.Modules.Add(new RecipeModule { Type = "WindForce" }.Set("Pull", 0.05f));
    recipe.Links.Deposit = new RecipeDeposit { Residue = "SlimedCondition", Threshold = 0.3f, Interval = 7 };
    recipe.Emitter.Radius = 40f;

    return recipe;
}

Section("the exported class");
VfxRecipe exportRecipe = ExportRecipe();
GeneratedShader exportShader = ShaderGenerator.Generate(exportRecipe, library);
string exportClass = FieldExporter.ContentClass(exportRecipe, exportShader.Uniforms, "TrelamiumIII.Content.Vfx", exportCatalog);

True("is a modular field", exportClass.Contains("internal sealed partial class IceExport : ModularField"));
True("is in the namespace it was given", exportClass.Contains("namespace TrelamiumIII.Content.Vfx;"));
True("says where it came from", exportClass.Contains("from Assets/Vfx/Recipes/IceExport.vfx.json"));
True("draws with its own shader", exportClass.Contains("Shader => Assets.Shaders.Vfx.IceExport.Asset;"));
True("carries the kind by name", exportClass.Contains("Kind = GasField.GasTypeID.Liquid,"));
True("carries physics", exportClass.Contains("Density = 0.36f,") && exportClass.Contains("MaxNodes = 250,"));
True("carries the temperature range", exportClass.Contains("TemperatureRange = new Vector2(0f, 1500f),"));
True("carries a float", exportClass.Contains(".Set(\"cut_Level\", 0.14f)"));
True("carries a pair", exportClass.Contains(".Set(\"noise_Drift\", new Vector2(0.05f, 0.015f))"));
True("carries a colour", exportClass.Contains(".Set(\"palette_Body\", new Vector3(0.62f, 0.8f, 0.92f))"));
True("asks for its noise only when it draws", exportClass.Contains(".Set(\"noise_NoiseTex\", static () => Assets.Textures.Noise.LiquidNoise.Asset)"));
True("every uniform is set", exportShader.Uniforms.All(uniform => exportClass.Contains($".Set(\"{uniform.Name}\", ")));
True("a tuned setting is what was tuned", exportClass.Contains("new WindForce { Pull = 0.05f, Scale = 1f },"));
True("and an untouched one is pinned to today's default", exportClass.Contains("Scale = 1f"));
True("modules bring their namespace", exportClass.Contains("using TrelamiumIII.Core.Graphics.Vfx.Modules;"));
True("the residue is a type", exportClass.Contains("Residue = typeof(SlimedCondition),"));
True("with its numbers", exportClass.Contains("ResidueThreshold = 0.3f,") && exportClass.Contains("ResidueInterval = 7,"));
True("and its namespace", exportClass.Contains("using TrelamiumIII.Core.Tiles.Conditions.Types;"));
Check("generating it twice comes out the same", FieldExporter.ContentClass(exportRecipe, exportShader.Uniforms, "TrelamiumIII.Content.Vfx", exportCatalog), exportClass);

// Every number in the file has to read back as exactly the float it was.
foreach (UniformValue uniform in exportShader.Uniforms.Where(static uniform => uniform.Kind == ParamKind.Float))
{
    int start = exportClass.IndexOf($".Set(\"{uniform.Name}\", ", StringComparison.Ordinal) + uniform.Name.Length + 9;
    string literal = exportClass[start..exportClass.IndexOf("f)", start, StringComparison.Ordinal)];

    Check($"{uniform.Name} survives as a literal", float.Parse(literal, System.Globalization.CultureInfo.InvariantCulture), uniform.Values[0]);
}

Section("a field with less in it");
VfxRecipe plainRecipe = ExportRecipe();
plainRecipe.Modules.Clear();
plainRecipe.Links.Deposit = null;
string plainClass = FieldExporter.ContentClass(plainRecipe, ShaderGenerator.Generate(plainRecipe, library).Uniforms, "TrelamiumIII.Content.Vfx", exportCatalog);
True("no modules, no modules line", !plainClass.Contains("Modules"));
True("no residue, no residue lines", !plainClass.Contains("Residue"));
True("and no using it doesn't need", !plainClass.Contains("Conditions.Types") && !plainClass.Contains("Vfx.Modules"));

Section("textures");

string NoiseLine(string texture)
{
    VfxRecipe recipe = Stack(Stage("noise", "NoiseLayer").Set("Noise", texture));
    string text = FieldExporter.ContentClass(recipe, ShaderGenerator.Generate(recipe, library).Uniforms, "N", exportCatalog);

    return text.Split('\n').First(static line => line.Contains("noise_NoiseTex")).Trim();
}

Check("a noise texture, by name", NoiseLine("Milky_2"), ".Set(\"noise_NoiseTex\", static () => Assets.Textures.Noise.Milky_2.Asset),");
Check("another of the mod's, by its key", NoiseLine("TrelamiumIII/Assets/Textures/VFX/Star"), ".Set(\"noise_NoiseTex\", static () => Assets.Textures.VFX.Star.Asset),");
Check("a file name that can't be an identifier", NoiseLine("TrelamiumIII/Assets/Textures/VFX/Star-2"), ".Set(\"noise_NoiseTex\", static () => ModContent.Request<Texture2D>(\"TrelamiumIII/Assets/Textures/VFX/Star-2\")),");
Check("somebody else's", NoiseLine("Terraria/Images/Misc/Perlin"), ".Set(\"noise_NoiseTex\", static () => ModContent.Request<Texture2D>(\"Terraria/Images/Misc/Perlin\")),");

Section("what stops an export");

List<GenerationProblem> ProblemsFor(VfxRecipe recipe, ExportCatalog? catalog = null)
    => FieldExporter.Check(recipe, ShaderGenerator.Generate(recipe, library), catalog ?? exportCatalog).ToList();

Check("a good recipe has no problems", string.Join("; ", ProblemsFor(exportRecipe)), "");

VfxRecipe Named(string name)
{
    VfxRecipe recipe = ExportRecipe();
    recipe.Name = name;

    return recipe;
}

True("a class the mod already has", ProblemsFor(Named("MudField")).Single().IsError);
True("a keyword", ProblemsFor(Named("event")).Single().IsError);
True("a name the class itself reads", ProblemsFor(Named("Assets")).Single().IsError);
True("the shaders' own folder", ProblemsFor(Named("Vfx")).Single().IsError);
True("lowercase is otherwise fine", ProblemsFor(Named("bloodWisp")).Count == 0);

VfxRecipe missingTexture = Stack(Stage("noise", "NoiseLayer").Set("Noise", "Nope"));
GenerationProblem noTexture = ProblemsFor(missingTexture, new ExportCatalog { HasTexture = static key => !key.EndsWith("Nope") }).Single();
True("a texture that isn't there is an error", noTexture.IsError && noTexture.Message.Contains("Nope"), noTexture.ToString());

VfxRecipe strayModule = ExportRecipe();
strayModule.Modules.Add(new RecipeModule { Type = "Gone" });
GenerationProblem noModule = ProblemsFor(strayModule).Single();
True("a module this build lacks only warns, as the preview drops it too", !noModule.IsError && noModule.Where == "Gone", noModule.ToString());
True("and it's left out of the class", !FieldExporter.ContentClass(strayModule, exportShader.Uniforms, "N", exportCatalog).Contains("Gone"));

VfxRecipe straySetting = ExportRecipe();
straySetting.Modules[0].Set("Nope", 1f);
True("a setting a module lacks only warns", ProblemsFor(straySetting).Single() is { IsError: false } stray && stray.Message.Contains("Nope"));
True("and it's left out too", !FieldExporter.ContentClass(straySetting, exportShader.Uniforms, "N", exportCatalog).Contains("Nope"));

VfxRecipe strayResidue = ExportRecipe();
strayResidue.Links.Deposit!.Residue = "Gone";
True("a residue that's gone only warns", ProblemsFor(strayResidue).Single() is { IsError: false });
True("and leaves nothing, as the preview does", !FieldExporter.ContentClass(strayResidue, exportShader.Uniforms, "N", exportCatalog).Contains("Residue"));

Section("the hand-written half");
string handHalf = FieldExporter.HandWrittenHalf(exportRecipe, "TrelamiumIII.Content.Vfx");
True("is the other half of the class", handHalf.Contains("internal sealed partial class IceExport"));
True("doesn't repeat its base class", !handHalf.Contains("ModularField"));
True("is in the same namespace", handHalf.Contains("namespace TrelamiumIII.Content.Vfx;"));
True("keeps how it was released", handHalf.Contains("SpawnNode(position, angle.ToRotationVector2() * 1.5f, 40f, 180f);"), handHalf);
True("has one line ending throughout", !handHalf.Contains('\r'));
True("a set temperature comes along", FieldExporter.HandWrittenHalf(new VfxRecipe { Name = "Hot", Emitter = new RecipeEmitter { Temperature = 300f } }, "N").Contains(", temperature: 300f);"));

Section("the export stamp");
string stamped = ExportStamp.Stamp("line one\nline two\n");
True("a stamped file is intact", ExportStamp.IsIntact(stamped));
True("with other line endings too", ExportStamp.IsIntact(stamped.Replace("\n", "\r\n")));
True("with a byte order mark too", ExportStamp.IsIntact("\uFEFF" + stamped));
True("an edit shows", !ExportStamp.IsIntact(stamped + "// mine\n"));
True("a file that was never stamped isn't intact", !ExportStamp.IsIntact("line one\nline two\n"));
True("nor is an empty one", !ExportStamp.IsIntact(string.Empty));
Check("stamping is the same whichever line endings it's given", ExportStamp.Stamp("a\r\nb\r\n"), ExportStamp.Stamp("a\nb\n"));

Section("the folder");
Check("a plain folder", ExportPlan.FolderProblem("Content/Vfx"), null);
Check("the other slash, and stray ones", ExportPlan.CleanFolder(" \\Content\\Vfx/ "), "Content/Vfx");
True("not above the mod", ExportPlan.FolderProblem("../Outside") is not null);
True("not elsewhere on disk", ExportPlan.FolderProblem("C:/Outside") is not null);
True("not empty", ExportPlan.FolderProblem("") is not null);
True("not a name no namespace can have", ExportPlan.FolderProblem("Content/My Fields") is not null);

string exportRoot = Path.Combine(scratch, "export");
Directory.CreateDirectory(exportRoot);
Check("the namespace follows the folder", ExportPlan.NamespaceOf(exportRoot, "Content/Vfx", "IceExport"), "TrelamiumIII.Content.Vfx");
Check("nothing exported yet", ExportPlan.FindExisting(exportRoot, "IceExport"), null);

Section("reviewing and writing");
byte[] fakeCode = [1, 2, 3, 4];

List<ExportFile> FilesFor(VfxRecipe recipe, byte[]? code = null)
{
    GeneratedShader shader = ShaderGenerator.Generate(recipe, library);

    return ExportPlan.Files(recipe, shader, ExportStamp.Stamp(shader.Source!), code ?? fakeCode, "Content/Vfx", "TrelamiumIII.Content.Vfx", exportCatalog);
}

string Statuses(VfxRecipe recipe, byte[]? code = null) => string.Join(",", ExportPlan.Review(exportRoot, FilesFor(recipe, code)).Select(static row => row.Status));
string Exported(string path) => Path.Combine(exportRoot, path);

Check("five files, in the order they get written",
    string.Join(" ", FilesFor(exportRecipe).Select(static file => file.RelativePath)),
    "Assets/Vfx/Recipes/IceExport.vfx.json Assets/Shaders/Vfx/IceExport.fx Assets/Shaders/Vfx/IceExport.fxc Content/Vfx/IceExport.Vfx.g.cs Content/Vfx/IceExport.cs");
Check("all new at first", Statuses(exportRecipe), "New,New,New,New,New");
Check("writing writes all five", ExportPlan.Write(exportRoot, ExportPlan.Review(exportRoot, FilesFor(exportRecipe))), 5);
True("the class is stamped", ExportStamp.IsIntact(File.ReadAllText(Exported("Content/Vfx/IceExport.Vfx.g.cs"))));
True("so is the shader", ExportStamp.IsIntact(File.ReadAllText(Exported("Assets/Shaders/Vfx/IceExport.fx"))));
Check("the recipe reads back", RecipeFile.Serialize(RecipeFile.Load(Exported("Assets/Vfx/Recipes"), "IceExport")), RecipeFile.Serialize(exportRecipe));
Check("again, nothing to do", Statuses(exportRecipe), "Same,Same,Same,Same,Kept");
Check("so nothing is written", ExportPlan.Write(exportRoot, ExportPlan.Review(exportRoot, FilesFor(exportRecipe))), 0);
Check("the export can be found again", ExportPlan.FindExisting(exportRoot, "IceExport"), "Content/Vfx");

// Git hands files back with whatever line endings it likes.
string classPath = Exported("Content/Vfx/IceExport.Vfx.g.cs");
string asWritten = File.ReadAllText(classPath);
File.WriteAllText(classPath, asWritten.Replace("\n", "\r\n"));
Check("other line endings aren't a change", Statuses(exportRecipe), "Same,Same,Same,Same,Kept");
File.WriteAllText(classPath, asWritten);

VfxRecipe exportRetuned = ExportRecipe();
exportRetuned.Stages.First(static stage => stage.Id == "cut").Set("Level", 0.5f);
Check("a exportRetuned value leaves the shader alone", Statuses(exportRetuned), "Changed,Same,Same,Changed,Kept");

VfxRecipe restacked = ExportRecipe();
RecipeEdit.AddStage(restacked, "DomainWarp");
Check("a new stage changes the shader", Statuses(restacked, [9, 9]), "Changed,Changed,Changed,Changed,Kept");

File.AppendAllText(classPath, "// by hand\n");
Check("a hand edit to the class shows", Statuses(exportRetuned), "Changed,Same,Same,Edited,Kept");
File.WriteAllText(classPath, asWritten);

string shaderPath = Exported("Assets/Shaders/Vfx/IceExport.fx");
string shaderAsWritten = File.ReadAllText(shaderPath);
File.AppendAllText(shaderPath, "// by hand\n");
Check("so does one to the shader", Statuses(exportRecipe), "Same,Edited,Same,Same,Kept");
File.WriteAllText(shaderPath, shaderAsWritten);

string handPath = Exported("Content/Vfx/IceExport.cs");
File.AppendAllText(handPath, "// mine\n");
Check("a hand-written half is left out of it", Statuses(exportRecipe), "Same,Same,Same,Same,Kept");
ExportPlan.Write(exportRoot, ExportPlan.Review(exportRoot, FilesFor(exportRetuned)));
True("and survives another export", File.ReadAllText(handPath).EndsWith("// mine\n"));
True("while the class moved on", File.ReadAllText(classPath).Contains(".Set(\"cut_Level\", 0.5f)"));

byte[] stale = File.ReadAllBytes(Exported("Assets/Shaders/Vfx/IceExport.fxc"));
DateTime shaderTime = File.GetLastWriteTimeUtc(shaderPath);
Thread.Sleep(20);
ExportPlan.Write(exportRoot, ExportPlan.Review(exportRoot, FilesFor(ExportRecipe())));
Check("what didn't change isn't touched", File.GetLastWriteTimeUtc(shaderPath), shaderTime);
True("compiled bytes are compared as bytes", Statuses(ExportRecipe(), [.. stale, 5]) == "Same,Same,Changed,Same,Kept", Statuses(ExportRecipe(), [.. stale, 5]));

Check("capitalisation that doesn't match stops it", Statuses(Named("iceexport")).Contains("Misspelled"), true);
True("and writes nothing", ExportPlan.Review(exportRoot, FilesFor(Named("iceexport"))).Where(static row => row.Status == ExportStatus.Misspelled).All(static row => !row.WillWrite));

Section("moving exported files");
Directory.CreateDirectory(Exported("Content/Moved"));
File.Move(classPath, Exported("Content/Moved/IceExport.Vfx.g.cs"));
File.Move(handPath, Exported("Content/Moved/IceExport.cs"));
Check("the export is found where it went", ExportPlan.FindExisting(exportRoot, "IceExport"), "Content/Moved");
Check("its hand-written half decides the namespace, so the two stay one class", ExportPlan.NamespaceOf(exportRoot, "Content/Moved", "IceExport"), "TrelamiumIII.Content.Vfx");

Directory.CreateDirectory(Exported("obj/Debug"));
Directory.Delete(Exported("Content/Moved"), true);
File.WriteAllText(Exported("obj/Debug/IceExport.Vfx.g.cs"), string.Empty);
Check("a build's copy doesn't count", ExportPlan.FindExisting(exportRoot, "IceExport"), null);

Section("golden class");
string goldenClassPath = Path.Combine(tests, "Golden", "IceExport.Vfx.g.cs.txt");

if (updateGolden)
{
    File.WriteAllText(goldenClassPath, exportClass);
    Console.WriteLine("  (golden updated)");
}

Check("the class matches the golden file", exportClass, File.Exists(goldenClassPath) ? File.ReadAllText(goldenClassPath).Replace("\r\n", "\n") : "(no golden file)");

if (ShaderCompiler.FxcPath is not null)
{
    Section("exported shaders through fxc");
    string exportCache = Path.Combine(scratch, "export-fxc");
    string plainSource = ShaderGenerator.Generate(exportRecipe, library).Source!;
    string stampedSource = ExportStamp.Stamp(plainSource);

    ShaderCompileResult plainCompile = ShaderCompiler.CompileAsync(plainSource, exportCache).GetAwaiter().GetResult();
    ShaderCompileResult stampedCompile = ShaderCompiler.CompileAsync(stampedSource, exportCache).GetAwaiter().GetResult();

    True("the stamp doesn't reach the compiled shader", stampedCompile.Code is not null && stampedCompile.Code.SequenceEqual(plainCompile.Code!), stampedCompile.Log);

    // The cache names a source by its hash and the build by the recipe. The file's name mustn't matter, or the next build would rewrite the export.
    string shaderFolder = Path.Combine(scratch, "export-named");
    Directory.CreateDirectory(shaderFolder);
    string namedShader = Path.Combine(shaderFolder, "IceExport.fx");
    File.WriteAllText(namedShader, stampedSource);

    ShaderCompileResult named = ShaderCompiler.CompileFileAsync(namedShader, exportCache).GetAwaiter().GetResult();

    True("compiling under its real name gives the same bytes", named.Code is not null && named.Code.SequenceEqual(stampedCompile.Code!), named.Log);
}

Directory.Delete(scratch, true);

Console.WriteLine();
Console.WriteLine(failures == 0 ? $"All {passes} checks passed." : $"{failures} of {passes + failures} checks FAILED.");

return failures == 0 ? 0 : 1;
