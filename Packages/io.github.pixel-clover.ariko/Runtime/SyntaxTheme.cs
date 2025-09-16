using UnityEngine;

[CreateAssetMenu(fileName = "SyntaxTheme", menuName = "Ariko/Syntax Theme")]
public class SyntaxTheme : ScriptableObject
{
    public Color PlainTextColor = Color.white;
    public Color KeywordColor = Color.cyan;
    public Color TypeColor = Color.green;
    public Color StringColor = Color.yellow;
    public Color CommentColor = Color.gray;
    public Color NumberColor = Color.magenta;
}
