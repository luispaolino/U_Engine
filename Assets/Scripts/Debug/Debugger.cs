// Debugger.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class Debugger : MonoBehaviour
{
    [Header("System References")]
    public RoundSystem roundSystem;

    [Header("Colours")]
    public Color hurtboxColor = Color.blue;
    public Color hitboxIdleColor = Color.red;
    public Color hitboxActiveColor = Color.green;
    public Color fighterColor = Color.yellow;
    public Color distanceColor = new Color32(0x00, 0xFF, 0x9A, 0xFF);

    [Header("Visibility (Keys 3-7)")]
    public bool showHurtboxes = false;
    public bool showHitboxes = false;
    public bool showFighters = false;
    public bool showDistance = false;
    public bool showMetrics = false;

    [Header("Layer-Scanned Colliders (Auto-Populated)")]
    public Collider2D[] hurtboxTargets;
    public Collider2D[] hitboxTargets;
    public Collider2D[] fighterBodyColliders;

    [HideInInspector] public float distanceCircleRadius = 0.05f;
    [HideInInspector] public float distanceLabelOffset = 0.2f;
    [HideInInspector] public float lockY = 0f;

    private readonly Dictionary<Transform, float> groundY = new Dictionary<Transform, float>();
    private static Material _mat;

    void OnEnable()
    {
        if (_mat == null)
        {
            var sh = Shader.Find("Hidden/Internal-Colored");
            _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _mat.SetInt("_ZWrite", 0);
            _mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            _mat.renderQueue = 4000;
        }
        if (roundSystem == null && !Application.isPlaying)
        {
            roundSystem = FindFirstObjectByType<RoundSystem>();
        }
    }

    System.Collections.IEnumerator Start()
    {
        if (Application.isPlaying)
        {
            if (roundSystem == null)
            {
                roundSystem = FindFirstObjectByType<RoundSystem>();
            }
            if (roundSystem == null || roundSystem.Player1 == null || roundSystem.Player2 == null)
            {
                yield return null;
                if (roundSystem != null && (roundSystem.Player1 == null || roundSystem.Player2 == null))
                {
                    yield return null;
                }
            }
            if (roundSystem == null) { Debug.LogError("Debugger: RoundSystem could not be found!", this); }
            else if (roundSystem.Player1 == null || roundSystem.Player2 == null) { Debug.LogWarning("Debugger: Players not available from RoundSystem. Layer scan may be incomplete.", this); }
            
            LoadLayerObjects(); // Scan layers after attempting to link/wait
        }
        else if (!Application.isPlaying && enabled && gameObject.activeInHierarchy) // In Editor, only if active
        {
            // Only scan in editor if you explicitly want to see results based on scene setup
            // This can be useful, but also might pick up things you don't want if players aren't spawned.
            // LoadLayerObjects(); // Optional: call for editor preview when script is enabled
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha3)) showHurtboxes = !showHurtboxes;
        if (Input.GetKeyDown(KeyCode.Alpha4)) showHitboxes = !showHitboxes;
        if (Input.GetKeyDown(KeyCode.Alpha5)) showFighters = !showFighters;
        if (Input.GetKeyDown(KeyCode.Alpha6)) showDistance = !showDistance;
        if (Input.GetKeyDown(KeyCode.Alpha7)) showMetrics = !showMetrics;
    }

    // ... (OnRenderObject, OnDrawGizmos, OnGUI methods remain the same) ...
    void OnRenderObject() { if (_mat == null || !isActiveAndEnabled || roundSystem == null) return; _mat.SetPass(0); FighterCharacter p1 = roundSystem.Player1; FighterCharacter p2 = roundSystem.Player2; DrawHurtboxes(); DrawHitboxes(); DrawFighterBodyColliders(); if (showDistance && p1 != null && p2 != null) DrawHorizontalDistanceGL(p1.transform, p2.transform); if (showDistance) DrawJumpLinesGL(p1?.transform, p2?.transform); }
#if UNITY_EDITOR
    void OnDrawGizmos() { if (!showDistance || !isActiveAndEnabled) return; if (roundSystem == null && !Application.isPlaying) roundSystem = FindFirstObjectByType<RoundSystem>(); if (roundSystem == null) return; Transform p1RootGizmo = roundSystem.Player1?.transform; Transform p2RootGizmo = roundSystem.Player2?.transform; Handles.color = distanceColor; if (p1RootGizmo != null && p2RootGizmo != null) { Vector3 pa = new(p1RootGizmo.position.x, lockY, p1RootGizmo.position.z); Vector3 pb = new(p2RootGizmo.position.x, lockY, p2RootGizmo.position.z); Vector3 midH = (pa+pb)*0.5f; Handles.DrawSolidDisc(pa,Vector3.forward,distanceCircleRadius); Handles.DrawSolidDisc(pb,Vector3.forward,distanceCircleRadius); Handles.DrawWireDisc(pa,Vector3.forward,distanceCircleRadius); Handles.DrawWireDisc(pb,Vector3.forward,distanceCircleRadius); Gizmos.DrawLine(pa,pb); Handles.Label(midH + Vector3.down*distanceLabelOffset, Vector3.Distance(pa,pb).ToString("F2")); } foreach (var rt in new[]{p1RootGizmo, p2RootGizmo}) { if(!rt)continue; if(rt.position.y <= lockY+0.01f)continue; Vector3 bP=new(rt.position.x,lockY,rt.position.z); Vector3 rP=rt.position; Vector3 mJ=(bP+rP)*0.5f; Handles.DrawSolidDisc(rP,Vector3.forward,distanceCircleRadius); Handles.DrawWireDisc(rP,Vector3.forward,distanceCircleRadius); Gizmos.DrawLine(rP,bP); Handles.Label(mJ + Vector3.right*distanceLabelOffset, (rP.y-lockY).ToString("F2")); } }
#endif
    void OnGUI() { if (!isActiveAndEnabled || roundSystem == null) return; FighterCharacter p1 = roundSystem.Player1; FighterCharacter p2 = roundSystem.Player2; bool canDrawDistLabels = showDistance && Camera.main != null && p1 != null && p2 != null; if (canDrawDistLabels) { Vector3 pa = new(p1.transform.position.x, lockY, p1.transform.position.z); Vector3 pb = new(p2.transform.position.x, lockY, p2.transform.position.z); float hDist = Vector3.Distance(pa,pb); Vector3 midH = (pa+pb)*0.5f; Vector3 spH = Camera.main.WorldToScreenPoint(midH); if(spH.z>0){GUI.color=distanceColor; GUI.Label(new Rect(spH.x-30, Screen.height-spH.y-(distanceLabelOffset*30),60,20),hDist.ToString("F2")); GUI.color=Color.white;} foreach(var rt in new[]{p1?.transform, p2?.transform}){if(rt==null)continue; if(rt.position.y<=lockY+0.05f)continue; Vector3 bP=new(rt.position.x,lockY,rt.position.z); Vector3 rP=rt.position; float vH=rP.y-lockY; Vector3 mJ=(bP+rP)*0.5f; Vector3 scV=Camera.main.WorldToScreenPoint(mJ); if(scV.z>0){GUI.color=distanceColor; GUI.Label(new Rect(scV.x+(distanceLabelOffset*30),Screen.height-scV.y-10,60,20),vH.ToString("F2")); GUI.color=Color.white;}}} if (showMetrics && Application.isPlaying) { float topM=40f; float sideM=10f; float lineH=18f; float lineS=1f; float labelW=280f; int fontS=12; if (p1?.core != null) { GUIStyle sP1M=new GUIStyle(GUI.skin.label); sP1M.fontSize=fontS; sP1M.normal.textColor=Color.yellow; sP1M.alignment=TextAnchor.UpperLeft; float cY=topM; string hP1=string.Format("P1 Health: {0} / {1}",p1.core.Health,FighterCharacterCore.MAX_HEALTH); GUI.Label(new Rect(sideM,cY,labelW,lineH),hP1,sP1M); cY+=lineH+lineS; float cMP1=p1.core.CurrentMeterValue; float mMP1=FighterCharacterCore.METER_CAPACITY_FLOAT; float pP1=mMP1>0?(cMP1/mMP1)*100f:0f; string meterP1=string.Format("Meter: {0:F1} / {1:F0} ({2:F0}%)",cMP1,mMP1,pP1); GUI.Label(new Rect(sideM,cY,labelW,lineH),meterP1,sP1M); cY+=lineH+lineS; float crTP1=p1.core.CurrentCrushTimer; string lP1=string.Format("Lockout: {0:F2}s",crTP1); GUI.Label(new Rect(sideM,cY,labelW,lineH),lP1,sP1M); } if (p2?.core != null) { GUIStyle sP2M=new GUIStyle(GUI.skin.label); sP2M.fontSize=fontS; sP2M.normal.textColor=Color.yellow; sP2M.alignment=TextAnchor.UpperRight; float cY=topM; float rX_P2=Screen.width-labelW-sideM; string hP2=string.Format("P2 Health: {0} / {1}",p2.core.Health,FighterCharacterCore.MAX_HEALTH); GUI.Label(new Rect(rX_P2,cY,labelW,lineH),hP2,sP2M); cY+=lineH+lineS; float cMP2=p2.core.CurrentMeterValue; float mMP2=FighterCharacterCore.METER_CAPACITY_FLOAT; float pP2=mMP2>0?(cMP2/mMP2)*100f:0f; string meterP2=string.Format("Meter: {0:F1} / {1:F0} ({2:F0}%)",cMP2,mMP2,pP2); GUI.Label(new Rect(rX_P2,cY,labelW,lineH),meterP2,sP2M); cY+=lineH+lineS; float crTP2=p2.core.CurrentCrushTimer; string lP2=string.Format("Lockout: {0:F2}s",crTP2); GUI.Label(new Rect(rX_P2,cY,labelW,lineH),lP2,sP2M); } } }


    public void LoadLayerObjects()
    {
        Debug.Log("Debugger: Scanning layers...");

        // Clear existing targets to ensure a fresh scan
        hurtboxTargets = new Collider2D[0];
        hitboxTargets = new Collider2D[0];
        fighterBodyColliders = new Collider2D[0];
        groundY.Clear();

        int hurtL = LayerMask.NameToLayer("Hurtbox");
        int hitL = LayerMask.NameToLayer("Hitbox");
        int figL = LayerMask.NameToLayer("FighterPush");

        // Consider only finding active colliders if that's desired
        var allColliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None)
                                .Where(c => c.gameObject.activeInHierarchy && c.enabled).ToArray();

        hurtboxTargets = allColliders.Where(c => c.gameObject.layer == hurtL).ToArray();
        hitboxTargets = allColliders.Where(c => c.gameObject.layer == hitL).ToArray();
        fighterBodyColliders = allColliders.Where(c => c.gameObject.layer == figL).ToArray();
        
        Debug.Log($"Debugger: Found {hurtboxTargets.Length} hurtboxes, {hitboxTargets.Length} hitboxes, {fighterBodyColliders.Length} fighter bodies.");

        FighterCharacter p1_for_groundY = roundSystem?.Player1;
        FighterCharacter p2_for_groundY = roundSystem?.Player2;

        var currentRoots = new List<Transform>();
        if (p1_for_groundY?.transform != null) currentRoots.Add(p1_for_groundY.transform);
        if (p2_for_groundY?.transform != null) currentRoots.Add(p2_for_groundY.transform);

        foreach (var r in currentRoots)
        {
            if (r == null) continue;
            // Find a fighterBodyCollider associated with this root (player's main collider)
            var col = fighterBodyColliders.FirstOrDefault(c => c.transform.IsChildOf(r) || c.transform == r);
            groundY[r] = col ? col.bounds.min.y : r.position.y;
        }
    }

    // ... (GL Drawing methods remain the same) ...
    void DrawHorizontalDistanceGL(Transform p1R, Transform p2R) { if (p1R == null || p2R == null) return; Vector3 pa = new(p1R.position.x, lockY, p1R.position.z); Vector3 pb = new(p2R.position.x, lockY, p2R.position.z); DrawFilledCircleGL(pa); DrawCircleOutlineGL(pa); DrawFilledCircleGL(pb); DrawCircleOutlineGL(pb); GL.Begin(GL.LINES); GL.Color(distanceColor); GL.Vertex(pa); GL.Vertex(pb); GL.End(); }
    void DrawJumpLinesGL(Transform p1R, Transform p2R) { foreach (var rt in new[]{p1R, p2R}) { if (!rt) continue; if (rt.position.y <= lockY + 0.05f) continue; Vector3 basePos = new(rt.position.x, lockY, rt.position.z); Vector3 rootPos = rt.position; DrawFilledCircleGL(rootPos); DrawCircleOutlineGL(rootPos); GL.Begin(GL.LINES); GL.Color(distanceColor); GL.Vertex(rootPos); GL.Vertex(basePos); GL.End(); } }
    void DrawFilledCircleGL(Vector3 c,float r=0,int seg=24) { if (r==0) r=distanceCircleRadius; GL.Begin(GL.TRIANGLES); GL.Color(new Color(distanceColor.r,distanceColor.g,distanceColor.b,0.5f)); for(int i=0;i<seg;i++) { float t1=2*Mathf.PI*i/seg,t2=2*Mathf.PI*(i+1)/seg; GL.Vertex(c); GL.Vertex(c+new Vector3(Mathf.Cos(t1),Mathf.Sin(t1))*r); GL.Vertex(c+new Vector3(Mathf.Cos(t2),Mathf.Sin(t2))*r); } GL.End(); }
    void DrawCircleOutlineGL(Vector3 c,float r=0,int seg=24) { if(r==0) r=distanceCircleRadius; GL.Begin(GL.LINES); GL.Color(distanceColor); for(int i=0;i<seg;i++) { float t1=2*Mathf.PI*i/seg,t2=2*Mathf.PI*(i+1)/seg; GL.Vertex(c+new Vector3(Mathf.Cos(t1),Mathf.Sin(t1))*r); GL.Vertex(c+new Vector3(Mathf.Cos(t2),Mathf.Sin(t2))*r); } GL.End(); }
    void DrawBoxSolid(Bounds b) { float z=b.center.z; GL.Vertex3(b.min.x,b.min.y,z); GL.Vertex3(b.max.x,b.min.y,z); GL.Vertex3(b.max.x,b.max.y,z); GL.Vertex3(b.min.x,b.max.y,z); }
    void DrawBoxOutline(Bounds b) { float z=b.center.z; Vector3 bl=new(b.min.x,b.min.y,z); Vector3 br=new(b.max.x,b.min.y,z); Vector3 tr=new(b.max.x,b.max.y,z); Vector3 tl=new(b.min.x,b.max.y,z); GL.Vertex(bl);GL.Vertex(br); GL.Vertex(br);GL.Vertex(tr); GL.Vertex(tr);GL.Vertex(tl); GL.Vertex(tl);GL.Vertex(bl); }
    void DrawHurtboxes() { if(!showHurtboxes||hurtboxTargets==null)return; var fill=new Color(hurtboxColor.r,hurtboxColor.g,hurtboxColor.b,.5f); GL.Begin(GL.QUADS);GL.Color(fill); foreach(var c in hurtboxTargets)if(c!=null && c.enabled && c.gameObject.activeInHierarchy)DrawBoxSolid(c.bounds); GL.End(); GL.Begin(GL.LINES);GL.Color(hurtboxColor); foreach(var c in hurtboxTargets)if(c!=null && c.enabled && c.gameObject.activeInHierarchy)DrawBoxOutline(c.bounds); GL.End(); }
    void DrawHitboxes() { if (!showHitboxes || hitboxTargets == null) return; foreach (var hb in hitboxTargets) { if (hb == null || !hb.enabled || !hb.gameObject.activeInHierarchy) continue; List<Collider2D> hitResults = new List<Collider2D>(); ContactFilter2D filter = new ContactFilter2D(); filter.useTriggers = true; bool hit = hb.Overlap(filter, hitResults) > 0; var cFill = (hit ? hitboxActiveColor : hitboxIdleColor); cFill.a = .5f; var cBorder = hit ? hitboxActiveColor : hitboxIdleColor; GL.Begin(GL.QUADS); GL.Color(cFill); DrawBoxSolid(hb.bounds); GL.End(); GL.Begin(GL.LINES); GL.Color(cBorder); DrawBoxOutline(hb.bounds); GL.End(); } }
    void DrawFighterBodyColliders() { if(!showFighters||fighterBodyColliders==null)return; var fill=fighterColor;fill.a=.3f; GL.Begin(GL.QUADS);GL.Color(fill); foreach(var c in fighterBodyColliders)if(c!=null && c.enabled && c.gameObject.activeInHierarchy)DrawBoxSolid(c.bounds); GL.End(); GL.Begin(GL.LINES);GL.Color(fighterColor); foreach(var c in fighterBodyColliders)if(c!=null && c.enabled && c.gameObject.activeInHierarchy)DrawBoxOutline(c.bounds); GL.End(); }
}


#if UNITY_EDITOR
// RENAMED Custom Editor Class
[CustomEditor(typeof(Debugger))]
public class DebuggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        Debugger dbg = (Debugger)target;

        // Removed the "Scan Layers" button as it's now automatic in Start.
        // You could add it back if manual re-scan during edit mode (not playing) is desired.
        // if (GUILayout.Button("Manually Scan Layers Now"))
        // {
        //     if (dbg != null)
        //     {
        //         dbg.LoadLayerObjects();
        //         if (!EditorApplication.isPlaying && target != null) EditorUtility.SetDirty(target);
        //     }
        // }

        if (dbg != null && dbg.roundSystem == null)
        {
            if (GUILayout.Button("Attempt to Find RoundSystem"))
            {
                dbg.roundSystem = FindFirstObjectByType<RoundSystem>();
                if (!EditorApplication.isPlaying && target != null) EditorUtility.SetDirty(target);
            }
        }
    }
}
#endif