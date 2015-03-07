/// This is the only script in this project.
/// written in very rough procedural way, needs deep refactoring
/// I've added comments to explain the algorithm

using UnityEngine;
using System.Collections;

/// <summary>
/// drag this on MainCamera game object in Unity3D scene
/// </summary>
public class LolController : MonoBehaviour {
    Vector3 value; // accumulator for accelerator emulation in unity editor
    WebCamTexture webcam; // to access photo camera of the phone
    GameObject container; // camera parent
    int rowSkip = 8; // when comparing two snapshots skip horizontal rows (columns more valuable)
    float turnpower = 64; // multiplier for yaw movement
    float sensorsens = 16; // offset multiplier. offset is applied to a previous frame to compare with the current frame
    bool showConfig = false; // used by gui to show menu with params
    bool showLog = false; // used by gui to show scheme of difference areas

    /// <summary>
    /// GUI to change params and display debug info (matrix of characters)
    /// </summary>
    void OnGUI () {
        GUILayout.BeginArea(new Rect(0, 0, Screen.width/2, Screen.height));
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Config")) showConfig = !showConfig;        
        GUILayout.Label("                                         ");
        GUILayout.Label("                                         ");
        GUILayout.Label("                                         ");
        GUILayout.Label("                                         ");
        GUILayout.Label("                                         ");
        GUILayout.EndHorizontal();
        if (showConfig) {
            GUILayout.Label("");
            GUILayout.Label("");
            GUILayout.Label("");
            Slider(1, 300, ref turnpower, "Yaw Speed " + (int)turnpower);
            GUILayout.Label("");
            Slider(2, 64, ref sensorsens, "Sensor Speed " + (int)sensorsens);
            GUILayout.Label("");
            float rowskp = rowSkip;
            Slider(1, 64, ref rowskp, "Row Skip " + (int)rowskp);
            GUILayout.Label("");
            rowSkip = (int) rowskp;
            if (rowSkip < 1) rowSkip = 1;
            if (GUILayout.Button(showLog?"Hide Log":"Show Log")) {
                showLog = !showLog;
                showConfig = false;
            }
            if (GUILayout.Button("Swap Eyes")) {
                var p1 = transform.GetChild(0).transform.localPosition;
                var p2 = transform.GetChild(1).transform.localPosition;
                var tmp = p1.x;
                p1.x = p2.x;
                p2.x = tmp;
                transform.GetChild(0).transform.localPosition = p1;
                transform.GetChild(1).transform.localPosition = p2;
            }
        } else if (showLog) {
            if (diffs != null) {
                for (int y = 0; y < diffs.GetLength(1); y++) {
                    GUILayout.BeginHorizontal();
                    for (int x = 0; x < diffs.GetLength(0); x++) {
                        if (x == bestX && y == bestY)
                            GUILayout.Label("@" + "\t");
                        else
                            GUILayout.Label(Did(diffs[x,y]/1000) + "\t");
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    /// <summary>
    /// Returns character that should visually display difference
    /// </summary>
    string Did(long val) {
        if (val > 500) return ".";
        if (val > 250) return ",";
        if (val > 125) return "-";
        if (val > 62) return "+";
        if (val > 32) return "o";
        return "O";
    }

    /// <summary>
    /// GUI slider template method
    /// </summary>
    void Slider(float min, float max, ref float val, string desc) {
        GUILayout.BeginHorizontal();
        GUILayout.Label(desc);
        val = GUILayout.HorizontalSlider(val, min, max);
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// Gets monochrome value from RGB (30%/60%/10%)
    /// </summary>
    int[] ColorsToLumi(Color32[] colors) {
        var lumi = new int[colors.Length];
        for (int i = 0; i < colors.Length; i++) {
            lumi[i] = colors[i].r * 3 + colors[i].g * 6 + colors[i].b;
        }
        return lumi;
    }

    /// <summary>
    /// This method rotates camera container around Y axis.
    /// Most complex part here.
    /// Takes snapshot from camera.
    /// Takes previous snapshot from memory.
    /// Shifts previous snapshot with different offsets by X and Y, computes general difference.
    /// Finds best offset that makes previous frame align with current frame.
    /// Judging from X offset creates rotation. Y offset is needed for better search of matching position.
    /// 
    /// pseudo graphical explanation:
    /// 
    /// current   ----I--
    /// previous  ---I---
    /// 
    /// best offset: X+1
    /// current   ----I--
    /// shft.prev. ---I---
    /// 
    /// 
    /// IN OTHER WORDS WE MOVE PREVIOUS FRAME OVER CURRENT FRAME UNTIL WE FIND POSITION WHEN TWO FRAMES MATCH
    /// 
    /// </summary>
    long[,] diffs;
    int bestX, bestY;
    int[] prevPixels;
    float smoothTP = 0;
    void DetectYaw() {
        var currentPixels = ColorsToLumi(webcam.GetPixels32());
        if (prevPixels != null) {
            int ou = (int) (webcam.width / sensorsens);
            if (ou < 1) ou = 1;
            int[] xdm = new int[] {    -3, -2, -1, 0, 1, 2, 3 };
            int[] ydm = new int[] {    -3, -2, -1, 0, 1, 2, 3 };
            diffs = new long[xdm.Length, ydm.Length];
            for (int x = 0; x < xdm.Length; x++) {
                for (int y = 0; y < ydm.Length; y++) {
                    diffs[x,y] = PixelDifference(currentPixels, prevPixels, 
                                                 xdm[x] * ou, ydm[y] * ou,
                                                 webcam.width, webcam.height);
                }
            }
            bestX = 0;
            bestY = 0;
            long less = long.MaxValue;
            for (int x = 0; x < xdm.Length; x++) {
                for (int y = 0; y < ydm.Length; y++) {
                    if (diffs[x,y] < less) {
                        less = diffs[x,y];
                        bestX = x;
                        bestY = y;
                    }
                }
            }
            float rot = xdm[bestX];
            rot += Input.GetAxisRaw("Mouse X") * 
                   (Input.GetMouseButton(0) ? 10 : 0);
            if (less == 0) rot = 0;
            Smooth (ref smoothTP, 10, ref rot);            
            container.transform.Rotate(Vector3.up, 
                                       rot * Time.deltaTime * turnpower);
        }
        prevPixels = currentPixels;
    }

    /// <summary>
    /// abs(a-b)
    /// </summary>
    int absd(int a, int b) {
        if (a > b) return a - b;
        if (b > a) return b - a;
        return 0;
    }

    /// <summary>
    /// accumulates difference for each pixel of two arrays
    /// </summary>
    /// <param name="original">current frame</param>
    /// <param name="shifted">previous frame that will be shifted during comparison with the params below</param>
    /// <param name="xShift">shift in pixels</param>
    /// <param name="yShift"></param>
    /// <param name="width">dimensions (same for both images)</param>
    /// <param name="height"></param>
    /// <returns></returns>
    long PixelDifference(int[] original, int[] shifted, 
                         int xShift, int yShift,
                         int width, int height) {
        long diff = 0;
        int y = 0;
        int c = original.Length;
        int p = 1;
        while(true) {
            for (int x = 0; x < width; x++) {
                int index = x + y * width; // index in array of pixels
                if (index >= c) return (1000*diff)/p; // if reached last pixel - return result 
                int index2 = (x + xShift) + (y + yShift) * width; // shift index
                if (index2 >= c) return (1000*diff)/p; // if reached last pixel - return result
                if (x + xShift > width) break; // skip if shift created position after end of line
                if (x + xShift < 0) { x += -xShift; continue; } // skip few iterations if shift created position before start of line
                if (y + yShift > height) break; // same for y axis
                if (y + yShift < 0) { y += -yShift; continue; }
                if (index < 0 || index2 < 0) continue; // prevent out of range access
                // actual comparison and accumulation (in diff)
                var col1 = original[index];
                var col2 = shifted[index2];
                diff += absd(col1, col2);
                p++; // accumulations counter
            }
            y += rowSkip; // skipping rows
        }
    }

    /// <summary>
    /// creates game objects hierarchy with virtual camera, access phone's camera
    /// I use 32x32 pixel resolution (blured pixels from texture mip map), that's enough and not too heavy
    /// </summary>
    void Start () {
        container = new GameObject();
        gameObject.transform.parent = container.transform;
        gameObject.transform.localPosition = Vector3.zero;
        container.transform.position = Vector3.up * 2;
        container.AddComponent<CharacterController>();
        webcam = new WebCamTexture(WebCamTexture.devices[0].name, 32, 32);
        webcam.Play();
    }

    /// <summary>
    /// Some strange smooth logic that changes two variables at time, can't remember why.
    /// </summary>
    void Smooth(ref float acc, float spd, ref float val) {
        acc = acc * Mathf.Clamp01(1 - Time.deltaTime * spd) + 
              val * (Mathf.Clamp01(Time.deltaTime * spd));
        val = acc;
    }

    float fspeed = 0;
    float smoothEAZ = 0, 
          smoothEAX = 0;
    // controls the camera using accelerometer and DetectYaw algorithm
    void Update () {
#if UNITY_EDITOR
        value += Vector3.up * Input.GetAxisRaw("Mouse Y") * 
                 (Input.GetMouseButton(0) ? 0 : 1);
        value += Vector3.forward * Input.GetAxisRaw("Mouse Y") * 
                 (Input.GetMouseButton(0) ? 1 : 0);
        value.Normalize();
#else
        value = Input.acceleration;
        value.Normalize();
#endif
        var ea = gameObject.transform.localEulerAngles;
        ea.z = value.x * -89;
        ea.x = value.z * -89;
        Smooth(ref smoothEAZ, 10, ref ea.z);
        Smooth(ref smoothEAX, 15, ref ea.x);
        if (value.z < -0.33f) fspeed = 2;
        if (value.z > 0.33f) fspeed = 0;
        if (value.z > 0.5f) fspeed = -2;
        if (fspeed > 0) {
            fspeed -= Time.deltaTime / 10;
            if (fspeed < 0) fspeed = 0;
        } else if (fspeed > 0) {
            fspeed += Time.deltaTime / 10;
            if (fspeed > 0) fspeed = 0;
        }
        container.GetComponent<CharacterController>().
                  SimpleMove(container.transform.forward * 
                  fspeed * 15 * Time.deltaTime);
        gameObject.transform.localEulerAngles = ea;
        DetectYaw();
    }
}
