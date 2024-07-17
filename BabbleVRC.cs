using System.Reflection;
using VRCFaceTracking.Core.Params.Expressions;

namespace VRCFaceTracking.Babble;

public class BabbleVRC : ExtTrackingModule
{
    private BabbleOSC babbleOSC;

    public override (bool SupportsEye, bool SupportsExpression) Supported => (false, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        Config babbleConfig = BabbleConfig.GetBabbleConfig();
        babbleOSC = new BabbleOSC(Logger, babbleConfig.Host, babbleConfig.Port);
        List<Stream> list = new List<Stream>();
        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        Stream manifestResourceStream = executingAssembly.GetManifestResourceStream("VRCFaceTracking.Babble.BabbleLogo.png")!;
        list.Add(manifestResourceStream);
        ModuleInformation = new ModuleMetadata
        {
            Name = "Project Babble Face Tracking\nInference Model v2.1.1",
            StaticImages = list
        };
        return (false, true);
    }

    public override void Teardown()
    {
        babbleOSC.Teardown();
    }

    public override void Update()
    {
        foreach (UnifiedExpressions expression in BabbleExpressions.BabbleExpressionMap)
        {
            UnifiedTracking.Data.Shapes[(int)expression].Weight = BabbleExpressions.BabbleExpressionMap.GetByKey1(expression);
        }
        Thread.Sleep(10);
    }
}
