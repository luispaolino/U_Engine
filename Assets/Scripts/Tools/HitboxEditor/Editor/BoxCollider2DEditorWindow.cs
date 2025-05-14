#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.Rendering;            // for PreviewRenderUtility

public sealed class BoxCollider2DEditorWindow : EditorWindow
{
    [MenuItem("Window/Fighting Game/Box Collider 2D Editor")]
    static void Open() => GetWindow<BoxCollider2DEditorWindow>("Box Collider 2D");

    // ── Inspector fields ────────────────────────────────────────────────────
    GameObject     _prefab;
    AnimationClip  _clip;
    ColliderClip2D _asset;
    float          _frameRate = 60f;
    int            _frameIndex;
    
    // ── Preview render util ─────────────────────────────────────────────────
    PreviewRenderUtility _previewUtil;
    GameObject           _previewGO;
    Animator             _anim;
    readonly BoxBoundsHandle _handle = new();

    void OnEnable()
    {
        _previewUtil = new PreviewRenderUtility();
        _previewUtil.cameraFieldOfView = 30f;
        SceneView.duringSceneGui += DrawSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSceneGUI;
        _previewUtil.Cleanup();
        if (_previewGO) DestroyImmediate(_previewGO);
    }

    void OnGUI()
    {
        // ── Top: prefab / clip / asset fields ─────────────────────────────
        EditorGUILayout.Space();
        _prefab = (GameObject)EditorGUILayout.ObjectField("Character Prefab",
                     _prefab, typeof(GameObject), false);
        _clip   = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip",
                     _clip, typeof(AnimationClip), false);

        if (_clip && !_asset)
        {
            if (GUILayout.Button("Create Clip Data (.asset)"))
                CreateNewAsset();
        }

        _asset = (ColliderClip2D)EditorGUILayout.ObjectField("Collider Clip Asset",
                     _asset, typeof(ColliderClip2D), false);

        // ── Middle: in-window preview ────────────────────────────────────────
        Rect previewRect = GUILayoutUtility.GetRect(300, 300, GUILayout.ExpandWidth(true));
        DrawPreview(previewRect);

        // ── Scrub slider & frame label ───────────────────────────────────────
        if (_asset != null && _clip != null && _prefab != null)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawTimeline();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ── Bottom: save button ──────────────────────────────────────────────
        if (_asset != null && _clip != null && _prefab != null)
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("Save Asset", GUILayout.Height(24)))
                EditorUtility.SetDirty(_asset);
        }
    }

    // ── Creates the ScriptableObject asset with one empty frame per clip frame
    void CreateNewAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Collider Clip",
            _clip.name + "_Hitboxes", "asset",
            "Where to save your hitbox data?");
        if (string.IsNullOrEmpty(path)) return;

        _asset           = CreateInstance<ColliderClip2D>();
        _asset.clip      = _clip;
        _asset.frameRate = Mathf.RoundToInt(_clip.frameRate);

        int total = Mathf.CeilToInt(_clip.length * _asset.frameRate);
        for (int i = 0; i < total; i++)
            _asset.frames.Add(new ColliderClip2D.Frame());

        AssetDatabase.CreateAsset(_asset, path);
        AssetDatabase.SaveAssets();
    }

    // ── Draw the scrub slider and handle arrow keys ────────────────────────
    void DrawTimeline()
    {
        int total = _asset.frames.Count;
        EditorGUILayout.LabelField($"Frame {_frameIndex+1} / {total}", EditorStyles.boldLabel);

        int newIndex = Mathf.RoundToInt(
            GUILayout.HorizontalSlider(_frameIndex, 0, total - 1, GUILayout.Width(200)));

        if (newIndex != _frameIndex)
        {
            _frameIndex = newIndex;
            Repaint();
        }

        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.LeftArrow)
                _frameIndex = Mathf.Max(0, _frameIndex - 1);
            if (Event.current.keyCode == KeyCode.RightArrow)
                _frameIndex = Mathf.Min(total - 1, _frameIndex + 1);
            Repaint();
        }
    }

    // ── Spawns (or reuses) the preview GameObject under PreviewRenderUtility  ─
    void EnsurePreviewInstance()
    {
        if (_previewGO != null) return;

        _previewGO = (GameObject)PrefabUtility.InstantiatePrefab(_prefab);
        _previewGO.hideFlags = HideFlags.HideAndDontSave;
        _anim = _previewGO.GetComponent<Animator>();
    }

    // ── Renders the current frame + hit-box into a texture & draws it ──────
    void DrawPreview(Rect r)
    {
        if (_prefab == null || _clip == null || _asset == null) return;

        EnsurePreviewInstance();

        // sample animation
        float t = _frameIndex / _asset.frameRate;
        AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(_previewGO, _clip, t);
        AnimationMode.EndSampling();
        AnimationMode.StopAnimationMode();

        // tell the preview util to render our prefab
        _previewUtil.BeginPreview(r, GUIStyle.none);
        _previewUtil.camera.transform.position = Vector3.back * 5f;
        _previewUtil.camera.transform.rotation = Quaternion.identity;
        _previewUtil.camera.clearFlags = CameraClearFlags.Color;
        _previewUtil.camera.backgroundColor = Color.gray;
        _previewUtil.lights[0].intensity = 1f;
        _previewUtil.lights[0].transform.rotation = Quaternion.Euler(50, 50, 0);
        _previewUtil.lights[1].intensity = 1f;

        _previewUtil.DrawMesh(_previewGO.GetComponentInChildren<MeshFilter>().sharedMesh,
                              _previewGO.transform.localToWorldMatrix,
                              _previewGO.GetComponentInChildren<MeshRenderer>().sharedMaterial,
                              0);

        // draw the box handle on top
        Handles.SetCamera(_previewUtil.camera);
        var frame = _asset.frames[_frameIndex];
        if (frame.boxes.Count == 0) frame.boxes.Add(new ColliderClip2D.Box());
        var box = frame.boxes[0];

        using (new Handles.DrawingScope(Matrix4x4.identity))
        {
            _handle.center = box.offset;
            _handle.size   = new Vector3(box.size.x, box.size.y, 0);
            EditorGUI.BeginChangeCheck();
            _handle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, "Edit Hitbox");
                box.offset = _handle.center;
                box.size   = new Vector2(_handle.size.x, _handle.size.y);
                EditorUtility.SetDirty(_asset);
            }
        }

        // finish and draw
        Texture tex = _previewUtil.EndPreview();
        GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
    }

    // ── (Left empty since we no longer pipe to SceneView directly) ───────────
    void DrawSceneGUI(SceneView sv) { }
}
#endif
