using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LanguageDefinition", menuName = "Ariko/Language Definition")]
public class LanguageDefinition : ScriptableObject
{
    public List<string> Keywords = new();
    public List<string> Types = new();

    [Header("Regex Patterns")] public string StringPattern = @"""""(.*?)""""";

    public string CommentPattern = @"//(.*?)$";
    public string NumberPattern = @"\b\d+(\.\d+)?f?\b";
}
