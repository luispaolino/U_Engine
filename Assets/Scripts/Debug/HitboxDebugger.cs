// HitboxDebugger.cs  – ONE script file
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class HitboxDebugger : MonoBehaviour
{
    // ───────── Colours ─────────
    public Color hurtboxColor      = Color.blue;
    public Color hitboxIdleColor   = Color.red;
    public Color hitboxActiveColor = Color.green;
    public Color fighterColor      = Color.yellow;
    public Color distanceColor     = new Color32(0x00,0xFF,0x9A,0xFF);

    // ───────── Visibility (keys 3-6) ─────────
    public bool showHurtboxes = false;
    public bool showHitboxes  = false;
    public bool showFighters  = false;
    public bool showDistance  = false;

    // ───────── Layer-scanned colliders ─────────
    public Collider2D[] hurtboxTargets;
    public Collider2D[] hitboxTargets;
    public Collider2D[] fighterTargets;

    // ───────── Distance-viz settings (hidden) ─────────
    [HideInInspector] public float distanceCircleRadius = 0.05f;
    [HideInInspector] public float distanceLabelOffset  = 0.2f;
    [HideInInspector] public float lockY = 0f;  // horizontal circles stay at this Y

    // ───────── Player roots ─────────
    public Transform player1Root;
    public Transform player2Root;

    // store bottom-of-collider Y (still helpful for ground reference)
    private readonly Dictionary<Transform,float> groundY = new();

    // shared GL material
    private static Material _mat;

    // ═════════════ INITIALISE MATERIAL ═════════════
    void OnEnable()
    {
        if (_mat != null) return;
        var sh = Shader.Find("Hidden/Internal-Colored");
        _mat = new Material(sh){ hideFlags = HideFlags.HideAndDontSave };
        _mat.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_Cull",    (int)UnityEngine.Rendering.CullMode.Off);
        _mat.SetInt("_ZWrite",  0);
        _mat.SetInt("_ZTest",   (int)UnityEngine.Rendering.CompareFunction.Always);
        _mat.renderQueue = 4000;
    }

    // ═════════════ KEY TOGGLES ═════════════
    void Update()
    {
        if (!Application.isPlaying) return;
        if (Input.GetKeyDown(KeyCode.Alpha3)) showHurtboxes = !showHurtboxes;
        if (Input.GetKeyDown(KeyCode.Alpha4)) showHitboxes  = !showHitboxes;
        if (Input.GetKeyDown(KeyCode.Alpha5)) showFighters  = !showFighters;
        if (Input.GetKeyDown(KeyCode.Alpha6)) showDistance  = !showDistance;
    }

    // ═════════════ MAIN GL PASS ═════════════
    void OnRenderObject()
    {
        if (_mat == null) return;
        _mat.SetPass(0);

        DrawHurtboxes();
        DrawHitboxes();
        DrawFighterBoxes();

        if (showDistance && player1Root && player2Root)
            DrawHorizontalDistanceGL();

        if (showDistance)
            DrawJumpLinesGL();
    }

#if UNITY_EDITOR
    // ═════════════ SCENE-VIEW GIZMOS ═════════════
    void OnDrawGizmos()
    {
        if (!showDistance) return;
        Handles.color = distanceColor;

        // horizontal distance (X-only)
        if (player1Root && player2Root)
        {
            Vector3 pa = new(player1Root.position.x, lockY, player1Root.position.z);
            Vector3 pb = new(player2Root.position.x, lockY, player2Root.position.z);
            Vector3 midHoriz = (pa + pb) * 0.5f;

            Handles.DrawSolidDisc(pa, Vector3.forward, distanceCircleRadius);
            Handles.DrawSolidDisc(pb, Vector3.forward, distanceCircleRadius);
            Handles.DrawWireDisc(pa, Vector3.forward, distanceCircleRadius);
            Handles.DrawWireDisc(pb, Vector3.forward, distanceCircleRadius);
            Gizmos.DrawLine(pa, pb);
            Handles.Label(midHoriz + Vector3.down * distanceLabelOffset,
                          Vector3.Distance(pa, pb).ToString("F2"));
        }

        // extra root-circles & vertical lines (only if root is above lockY)
        foreach (var rt in new[]{player1Root, player2Root})
        {
            if (!rt) continue;
            if (rt.position.y <= lockY + 0.01f) continue;

            Vector3 basePos = new(rt.position.x, lockY, rt.position.z);
            Vector3 rootPos = rt.position;
            Vector3 midJump = (basePos + rootPos) * 0.5f;

            Handles.DrawSolidDisc(rootPos, Vector3.forward, distanceCircleRadius);
            Handles.DrawWireDisc (rootPos, Vector3.forward, distanceCircleRadius);
            Gizmos.DrawLine(rootPos, basePos);
            Handles.Label(midJump + Vector3.right * distanceLabelOffset,
                          (rootPos.y - lockY).ToString("F2"));
        }
    }
#endif

    // ═════════════ GAME-VIEW LABELS ═════════════
    void OnGUI()
    {
        if (!showDistance || Camera.main == null || !player1Root || !player2Root) return;

        Vector3 pa = new(player1Root.position.x, lockY, player1Root.position.z);
        Vector3 pb = new(player2Root.position.x, lockY, player2Root.position.z);
        float   h  = Vector3.Distance(pa, pb);
        Vector3 midHoriz = (pa + pb) * 0.5f;
        Vector3 sp = Camera.main.WorldToScreenPoint(midHoriz);
        if (sp.z > 0)
        {
            GUI.color = distanceColor;
            GUI.Label(new Rect(sp.x - 30, Screen.height - sp.y + 15, 60, 20), h.ToString("F2"));
            GUI.color = Color.white;
        }

        // vertical labels on jump lines
        foreach (var rt in new[]{player1Root, player2Root})
        {
            if (!rt) continue;
            if (rt.position.y <= lockY + 0.05f) continue;

            Vector3 basePos = new(rt.position.x, lockY, rt.position.z);
            Vector3 rootPos = rt.position;
            float   v       = rootPos.y - lockY;
            Vector3 midJump = (basePos + rootPos) * 0.5f;
            Vector3 sce     = Camera.main.WorldToScreenPoint(midJump);
            if (sce.z > 0)
            {
                GUI.color = distanceColor;
                GUI.Label(new Rect(sce.x + 10, Screen.height - sce.y - 10, 60, 20), v.ToString("F2"));
                GUI.color = Color.white;
            }
        }
    }

    // ═════════════ LAYER SCAN ═════════════
    public void LoadLayerObjects()
    {
        int hurtL = LayerMask.NameToLayer("Hurtbox");
        int hitL  = LayerMask.NameToLayer("Hitbox");
        int figL  = LayerMask.NameToLayer("FighterPush");
        int stgL  = LayerMask.NameToLayer("StageWall");

        var all = FindObjectsOfType<Collider2D>();
        hurtboxTargets = all.Where(c => c.gameObject.layer == hurtL).ToArray();
        hitboxTargets  = all.Where(c => c.gameObject.layer == hitL ).ToArray();
        fighterTargets = all.Where(c => c.gameObject.layer == figL ).ToArray();

        var roots = fighterTargets.Select(c => c.transform.root).Distinct().ToList();
        if (roots.Count >= 2) { player1Root = roots[0]; player2Root = roots[1]; }

        groundY.Clear();
        foreach (var r in roots)
        {
            var col = fighterTargets.FirstOrDefault(c => c.transform.root == r);
            groundY[r] = col ? col.bounds.min.y : r.position.y;
        }
    }

    // ═════════════ Horizontal distance (GL) ═════════════
    void DrawHorizontalDistanceGL()
    {
        Vector3 pa = new(player1Root.position.x, lockY, player1Root.position.z);
        Vector3 pb = new(player2Root.position.x, lockY, player2Root.position.z);

        DrawFilledCircleGL(pa); DrawCircleOutlineGL(pa);
        DrawFilledCircleGL(pb); DrawCircleOutlineGL(pb);

        GL.Begin(GL.LINES); GL.Color(distanceColor); GL.Vertex(pa); GL.Vertex(pb); GL.End();
    }

    // ═════════════ Jump lines (GL) ═════════════
    void DrawJumpLinesGL()
    {
        foreach (var rt in new[]{player1Root, player2Root})
        {
            if (!rt) continue;
            if (rt.position.y <= lockY + 0.05f) continue;

            Vector3 basePos = new(rt.position.x, lockY, rt.position.z);
            Vector3 rootPos = rt.position;

            DrawFilledCircleGL(rootPos); DrawCircleOutlineGL(rootPos);
            GL.Begin(GL.LINES); GL.Color(distanceColor); GL.Vertex(rootPos); GL.Vertex(basePos); GL.End();
        }
    }

    // ═════════════ Tiny circle helpers (GL) ═════════════
    void DrawFilledCircleGL(Vector3 c,float r=0,int seg=24)
    {
        if (r==0) r=distanceCircleRadius;
        GL.Begin(GL.TRIANGLES);
        GL.Color(new Color(distanceColor.r,distanceColor.g,distanceColor.b,0.5f));
        for(int i=0;i<seg;i++)
        {
            float t1=2*Mathf.PI*i/seg,t2=2*Mathf.PI*(i+1)/seg;
            GL.Vertex(c);
            GL.Vertex(c+new Vector3(Mathf.Cos(t1),Mathf.Sin(t1))*r);
            GL.Vertex(c+new Vector3(Mathf.Cos(t2),Mathf.Sin(t2))*r);
        }
        GL.End();
    }
    void DrawCircleOutlineGL(Vector3 c,float r=0,int seg=24)
    {
        if(r==0) r=distanceCircleRadius;
        GL.Begin(GL.LINES); GL.Color(distanceColor);
        for(int i=0;i<seg;i++)
        {
            float t1=2*Mathf.PI*i/seg,t2=2*Mathf.PI*(i+1)/seg;
            GL.Vertex(c+new Vector3(Mathf.Cos(t1),Mathf.Sin(t1))*r);
            GL.Vertex(c+new Vector3(Mathf.Cos(t2),Mathf.Sin(t2))*r);
        }
        GL.End();
    }

    // ═════════════ Boxes (GL) ═════════════
    void DrawBoxSolid(Bounds b)
    {
        float z=b.center.z;
        GL.Vertex3(b.min.x,b.min.y,z);
        GL.Vertex3(b.max.x,b.min.y,z);
        GL.Vertex3(b.max.x,b.max.y,z);
        GL.Vertex3(b.min.x,b.max.y,z);
    }
    void DrawBoxOutline(Bounds b)
    {
        float z=b.center.z;
        Vector3 bl=new(b.min.x,b.min.y,z);
        Vector3 br=new(b.max.x,b.min.y,z);
        Vector3 tr=new(b.max.x,b.max.y,z);
        Vector3 tl=new(b.min.x,b.max.y,z);
        GL.Vertex(bl);GL.Vertex(br);
        GL.Vertex(br);GL.Vertex(tr);
        GL.Vertex(tr);GL.Vertex(tl);
        GL.Vertex(tl);GL.Vertex(bl);
    }

    void DrawHurtboxes()
    {
        if(!showHurtboxes||hurtboxTargets==null)return;
        var fill=new Color(hurtboxColor.r,hurtboxColor.g,hurtboxColor.b,.5f);
        GL.Begin(GL.QUADS);GL.Color(fill);
        foreach(var c in hurtboxTargets)DrawBoxSolid(c.bounds);
        GL.End();
        GL.Begin(GL.LINES);GL.Color(hurtboxColor);
        foreach(var c in hurtboxTargets)DrawBoxOutline(c.bounds);
        GL.End();
    }
    void DrawHitboxes()
    {
        if(!showHitboxes||hitboxTargets==null)return;
        foreach(var hb in hitboxTargets)
        {
            bool hit=hb.Overlap(new ContactFilter2D{useTriggers=true},new List<Collider2D>())>0;
            var fill=(hit?hitboxActiveColor:hitboxIdleColor);fill.a=.5f;
            var border=hit?hitboxActiveColor:hitboxIdleColor;
            GL.Begin(GL.QUADS);GL.Color(fill);DrawBoxSolid(hb.bounds);GL.End();
            GL.Begin(GL.LINES);GL.Color(border);DrawBoxOutline(hb.bounds);GL.End();
        }
    }
    void DrawFighterBoxes()
    {
        if(!showFighters||fighterTargets==null)return;
        var fill=fighterColor;fill.a=.3f;
        GL.Begin(GL.QUADS);GL.Color(fill);
        foreach(var c in fighterTargets)DrawBoxSolid(c.bounds);
        GL.End();
        GL.Begin(GL.LINES);GL.Color(fighterColor);
        foreach(var c in fighterTargets)DrawBoxOutline(c.bounds);
        GL.End();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(HitboxDebugger))]
public class HitboxDebuggerEditor:Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if(GUILayout.Button("Load Layers"))
        {
            var dbg=(HitboxDebugger)target;
            dbg.LoadLayerObjects();
            if(!EditorApplication.isPlaying)EditorUtility.SetDirty(dbg);
        }
    }
}
#endif
