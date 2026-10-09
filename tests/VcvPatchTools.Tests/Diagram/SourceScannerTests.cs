using VcvPatchTools.Core.Catalog;

namespace VcvPatchTools.Tests;

public class SourceScannerTests
{
    [Fact]
    public void EnumBodyExpandsEnumsMacroAndExplicitValues()
    {
        List<(string Id, int Index, string Fallback)> entries = SourceScanner.ParseEnumBody("""
            CLOCK_INPUT,
            ENUMS(CV_INPUTS, 3),
            RESET_INPUT = CV_INPUTS + 3,
            NUM_INPUTS
            """);

        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, entries.Select(e => e.Index));
        Assert.Equal("Clock", entries[0].Fallback);
        Assert.Equal("CV_INPUTS+2", entries[3].Id);
        Assert.Equal("Cv 3", entries[3].Fallback);
        Assert.Equal(("RESET_INPUT", 4, "Reset"), entries[4]);
    }

    [Fact]
    public void EnumBodyStopsAtUnknownExpression()
    {
        List<(string Id, int Index, string Fallback)> entries = SourceScanner.ParseEnumBody("INPUT_L, INPUT_R, MOD_INPUT, NUM_INPUTS = MOD_INPUT + n_mod_inputs");

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void StripCommentsKeepsUrlsInStrings()
    {
        string cleaned = SourceScanner.StripComments("""configInput(A, "http://x"); // gone""" + "\n/* gone */ B");

        Assert.Contains("\"http://x\"", cleaned);
        Assert.DoesNotContain("gone", cleaned);
    }

    [Fact]
    public void ScansPluginWithConfiguredAndHumanizedNames()
    {
        string manifest = """
            { "slug": "Demo", "modules": [ { "slug": "Env", "name": "Envelope", "tags": ["Envelope generator"] }, { "slug": "Ghost" } ] }
            """;
        string source = """
            struct Env : Module {
                enum InputIds { GATE_INPUT, ENUMS(CV_INPUTS, 2), NUM_INPUTS };
                enum OutputIds { ENV_OUTPUT, NUM_OUTPUTS };
                Env() {
                    configInput(GATE_INPUT, "Gate");
                    configInput(CV_INPUTS + 1, "Release CV");
                }
            };
            Model* modelEnv = createModel<Env, EnvWidget>("Env");
            """;

        ScanResult result = SourceScanner.ScanPlugin(manifest, new[] { new SourceFile("Env.cpp", source) });

        ModuleInfo env = result.Modules["Demo/Env"];
        Assert.Equal("Envelope", env.Name);
        Assert.Equal(new[] { "Gate", "Cv 1", "Release CV" }, env.Inputs);
        Assert.Equal(new[] { "Env" }, env.Outputs);
        Assert.Contains(result.Problems, p => p.StartsWith("Demo/Ghost", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvesSurgeStyleAliasAndModulationMatrix()
    {
        string manifest = """{ "slug": "SurgeXTRack", "modules": [ { "slug": "SurgeXTVCF", "name": "VCF" } ] }""";
        string header = """
            struct VCF : public modules::XTModule
            {
                static constexpr int n_mod_inputs{4};
                enum ParamIds { FREQUENCY, RESONANCE, VCF_MOD_PARAM_0, VCF_TYPE = VCF_MOD_PARAM_0 + 2 * n_mod_inputs, NUM_PARAMS };
                enum InputIds { INPUT_L, INPUT_R, VCF_MOD_INPUT, NUM_INPUTS = VCF_MOD_INPUT + n_mod_inputs };
                enum OutputIds { OUTPUT_L, OUTPUT_R, NUM_OUTPUTS };
                static int modulatorIndexFor(int baseParam, int modulator)
                {
                    int offset = baseParam - FREQUENCY;
                    return VCF_MOD_PARAM_0 + offset * n_mod_inputs + modulator;
                }
                modules::ModulationAssistant<VCF, 2, FREQUENCY, n_mod_inputs, VCF_MOD_INPUT> modulationAssistant;
                VCF()
                {
                    configParam<modules::VOctParamQuantity<60>>(FREQUENCY, -4, 6, 0, "Frequency");
                    configParam(RESONANCE, 0, 1, 0.7, "Resonance", "%", 0.f, 100.f);
                    configInput(INPUT_L, "Left");
                }
            };
            """;
        string widget = """
            struct VCFWidget : widgets::XTModuleWidget
            {
                typedef vcf::VCF M;
            };
            rack::Model *modelSurgeVCF = rack::createModel<sst::surgext_rack::vcf::ui::VCFWidget::M,
                                                           sst::surgext_rack::vcf::ui::VCFWidget>("SurgeXTVCF");
            """;

        ScanResult result = SourceScanner.ScanPlugin(manifest, new[] { new SourceFile("VCF.h", header), new SourceFile("VCF.cpp", widget) });

        ModuleInfo vcf = result.Modules["SurgeXTRack/SurgeXTVCF"];
        Assert.Equal((2, 4, 2), (vcf.Modulation!.FirstInput, vcf.Modulation.InputCount, vcf.Modulation.FirstDepthParam));
        Assert.Equal(new[] { "Frequency", "Resonance" }, vcf.Modulation!.Targets);
        Assert.Equal("Modulation Signal 4", vcf.Inputs[5]);
    }

    [Fact]
    public void ResolvesConstantSizesTemplateArgsAndLoopNames()
    {
        string manifest = """{ "slug": "P", "modules": [ { "slug": "Mix" }, { "slug": "Drone" } ] }""";
        string source = """
            struct Voice { static const int NUM_OSC = 3; };
            template<int N_TRK, int N_GRP>
            struct Mixer : Module {
                enum InputIds { ENUMS(TRACK_INPUTS, N_TRK * 2), ENUMS(GROUP_INPUTS, N_GRP), NUM_INPUTS };
                enum OutputIds { MAIN_OUTPUT, NUM_OUTPUTS };
            };
            struct Drone : Module {
                static const int NUM_OSC = Voice::NUM_OSC;
                enum InputIds { ENUMS(TRIG_INPUT, NUM_OSC), GATE_INPUT, NUM_INPUTS };
                enum OutputIds { ENUMS(OUT_OUTPUT, NUM_OSC), NUM_OUTPUTS };
                Drone() {
                    static const char* labels[NUM_OSC] = { "Low", "Mid", "High" };
                    for (int i = 0; i < NUM_OSC; i++) {
                        configInput(TRIG_INPUT + i, string::f("Oscillator %d trigger", i + 1));
                        configOutput(OUT_OUTPUT + i, labels[i]);
                    }
                    configInput(GATE_INPUT, "Gate");
                }
            };
            Model* modelMix = createModel<Mixer<4, 1>, MixWidget>("Mix");
            Model* modelDrone = createModel<Drone, DroneWidget>("Drone");
            """;

        ScanResult result = SourceScanner.ScanPlugin(manifest, new[] { new SourceFile("all.cpp", source) });

        Assert.Equal(9, result.Modules["P/Mix"].Inputs.Count);
        Assert.Equal(new[] { "Oscillator 1 trigger", "Oscillator 2 trigger", "Oscillator 3 trigger", "Gate" }, result.Modules["P/Drone"].Inputs);
        Assert.Equal(new[] { "Low", "Mid", "High" }, result.Modules["P/Drone"].Outputs);
    }
}