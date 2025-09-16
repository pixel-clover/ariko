using System;
using System.Collections.Generic;

[Serializable]
public class ToolCall
{
    public string thought;
    public string tool_name;
    public Dictionary<string, object> parameters;
}
