namespace Inno.VisualNovel.Model.Story;

/// <summary>Defines stable built-in node identifiers independent of CLR implementation names.</summary>
public static class StoryNodeIds
{
    /// <summary>Identifies a linear pass-through node.</summary>
    public const string entry = "inno.vn.entry";
    /// <summary>Identifies a completed story path.</summary>
    public const string end = "inno.vn.end";
    /// <summary>Identifies a dialogue or narration instruction.</summary>
    public const string dialogue = "inno.vn.dialogue";
    /// <summary>Identifies a player choice.</summary>
    public const string choice = "inno.vn.choice";
    /// <summary>Identifies a typed variable write.</summary>
    public const string assign = "inno.vn.assign";
    /// <summary>Identifies a conditional branch.</summary>
    public const string condition = "inno.vn.condition";
    /// <summary>Identifies a timed wait.</summary>
    public const string wait = "inno.vn.wait";
    /// <summary>Identifies an invocation with a return address.</summary>
    public const string call = "inno.vn.call";
    /// <summary>Identifies a return from an invocation.</summary>
    public const string returnFromCall = "inno.vn.return";
    /// <summary>Identifies a deterministic random branch.</summary>
    public const string random = "inno.vn.random";
    /// <summary>Identifies an author assertion.</summary>
    public const string assertion = "inno.vn.assert";
    /// <summary>Identifies an engine-routed diagnostic observation.</summary>
    public const string log = "inno.vn.log";
    /// <summary>Identifies a declarative stage or layered-character request.</summary>
    public const string stage = "inno.vn.stage";
    /// <summary>Identifies declarative sound playback, control and completion waits.</summary>
    public const string audio = "inno.vn.audio";
    /// <summary>Identifies a reusable performance lifecycle request.</summary>
    public const string performance = "inno.vn.performance";
    /// <summary>Identifies bounded Unicode player text input.</summary>
    public const string input = "inno.vn.input";
    /// <summary>Identifies an explicit collection unlock that is retained across rollback.</summary>
    public const string unlock = "inno.vn.unlock";
    /// <summary>Identifies an explicitly registered typed C# command.</summary>
    public const string command = "inno.vn.command";
    /// <summary>Identifies a bounded parallel presentation fork.</summary>
    public const string parallel = "inno.vn.parallel";
    /// <summary>Identifies the explicitly owned end of parallel tasks.</summary>
    public const string join = "inno.vn.join";
    /// <summary>Identifies deterministic screen-image operations.</summary>
    public const string screenEffect = "inno.vn.screen-effect";
    /// <summary>Identifies an explicit two-composition transition boundary.</summary>
    public const string transition = "inno.vn.transition";
    /// <summary>Identifies a no-code page, theme or title action owned by the session.</summary>
    public const string system = "inno.vn.system";
}
