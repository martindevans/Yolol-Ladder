using System;

namespace YololCompetition
{
    /// <summary>
    /// Hide the given command/module from the help interface
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HiddenAttribute
        : Attribute
    {
    }
}
