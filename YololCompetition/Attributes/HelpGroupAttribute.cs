using System;

namespace YololCompetition.Attributes
{
    public class HelpGroupAttribute
        : Attribute
    {
        public string GroupId { get; }

        public HelpGroupAttribute(string groupId)
        {
            GroupId = groupId;
        }
    }
}
