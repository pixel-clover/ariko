using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ArikoSearchWindow : EditorWindow
{
    private const int MaxResultsToDisplay = 50;
    public static Action<Object> OnAssetSelected;
    private List<Object> allAssets;
    private List<Object> filteredAssets;
    private Vector2 scrollPosition;
    private string searchText = "";

    private void OnEnable()
    {
        allAssets = new List<Object>();
        var guids = AssetDatabase.FindAssets("t:MonoScript t:TextAsset t:Prefab t:Shader");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset != null) allAssets.Add(asset);
        }

        allAssets = allAssets.OrderBy(a => a.name).ToList();
        FilterAssets(); // Initial filter
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Find Asset to Attach", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        searchText = EditorGUILayout.TextField("Search:", searchText, GUI.skin.FindStyle("ToolbarSearchTextField"));
        if (EditorGUI.EndChangeCheck()) FilterAssets();

        EditorGUILayout.Space();

        // Display a summary of the search results
        if (filteredAssets.Count > MaxResultsToDisplay)
            EditorGUILayout.LabelField($"Showing first {MaxResultsToDisplay} of {filteredAssets.Count} results.",
                EditorStyles.miniLabel);
        else
            EditorGUILayout.LabelField($"{filteredAssets.Count} result(s) found.", EditorStyles.miniLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Use .Take() to limit the number of items being rendered for performance
        foreach (var asset in filteredAssets.Take(MaxResultsToDisplay))
            if (GUILayout.Button(new GUIContent(asset.name, AssetDatabase.GetAssetPath(asset)), EditorStyles.label))
            {
                OnAssetSelected?.Invoke(asset);
                Close();
            }

        EditorGUILayout.EndScrollView();
    }

    public static void ShowWindow()
    {
        var window = GetWindow<ArikoSearchWindow>(true, "Add File to Context");
        window.minSize = new Vector2(300, 400);
        window.maxSize = new Vector2(300, 800);
    }

    private void FilterAssets()
    {
        if (string.IsNullOrEmpty(searchText))
        {
            filteredAssets = new List<Object>(allAssets);
        }
        else
        {
            var searchLower = searchText.ToLower();
            filteredAssets = allAssets.Where(a => a.name.ToLower().Contains(searchLower)).ToList();
        }
    }
}
