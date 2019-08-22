using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera), typeof(Jitter))]
public class VelocityBuffer : MonoBehaviour
{
    private Camera sceneCamera;
    private Jitter jitter;

    public Shader velocityShader;
    private Material velocityTexture;
    private RenderTexture velocityBuffer;

    private Matrix4x4 previousViewMat;

    public RenderTexture activeVelocityBuffer { get { return velocityBuffer; } }

    private float nextFrame;

    void Reset()
    {
        sceneCamera = GetComponent<Camera>();
        jitter = GetComponent<Jitter>();
    }

    void Awake()
    {
        Reset();
    }

    void OnPreRender()
    {
        if ((sceneCamera.depthTextureMode & DepthTextureMode.Depth) == 0)
        {
            sceneCamera.depthTextureMode |= DepthTextureMode.Depth;
        }
    }

    void OnPostRender()
    {
        // assign a render texture to the velocity buffer
        if (velocityBuffer == null)
        {
            velocityBuffer = RenderTexture.GetTemporary(sceneCamera.pixelWidth, sceneCamera.pixelHeight, 16, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Default, 1);
            velocityBuffer.filterMode = FilterMode.Point;
            velocityBuffer.wrapMode = TextureWrapMode.Clamp;
        }

        Matrix4x4 currentViewMat = sceneCamera.worldToCameraMatrix;
        Matrix4x4 currentProjectionMat = GL.GetGPUProjectionMatrix(sceneCamera.projectionMatrix, true);
        Matrix4x4 currentProjectionMat_NoFlip = GL.GetGPUProjectionMatrix(sceneCamera.projectionMatrix, false);

        RenderTexture activeRT = RenderTexture.active;
        RenderTexture.active = velocityBuffer;
        
        GL.Clear(true, true, Color.black);

        if (velocityTexture == null)
        {
            velocityTexture = new Material(velocityShader);
        } else {
            velocityTexture.hideFlags = HideFlags.DontSave; // object will not be saved to scene, will not be destroyed on new scene
        }

        velocityTexture.SetMatrix("currentViewMat", previousViewMat);
        velocityTexture.SetMatrix("currentViewProjectMat", currentProjectionMat * currentViewMat);
        velocityTexture.SetMatrix("previousViewProjectMat", currentProjectionMat * previousViewMat);
        velocityTexture.SetMatrix("previousViewProjectMat_NoFlip", currentProjectionMat_NoFlip * previousViewMat);
        velocityTexture.SetVector("projectionExtents", sceneCamera.GetProjectionExtents(jitter.currentSample.x, jitter.currentSample.y));

        previousViewMat = currentViewMat;

        velocityTexture.SetPass(0);
        DrawFullscreenQuad();

        RenderTexture.active = activeRT;
    }

    void OnApplicationQuit()
    {
        RenderTexture.ReleaseTemporary(velocityBuffer); // manually release velocity bugger
    }


    /// NOTE: Cite Pedersen

    public void DrawFullscreenQuad()
    {
        GL.PushMatrix();
        GL.LoadOrtho();
        GL.Begin(GL.QUADS);
        {
            GL.MultiTexCoord2(0, 0.0f, 0.0f);
            GL.Vertex3(0.0f, 0.0f, 0.0f); // bottom left

            GL.MultiTexCoord2(0, 1.0f, 0.0f);
            GL.Vertex3(1.0f, 0.0f, 0.0f); // bottom right

            GL.MultiTexCoord2(0, 1.0f, 1.0f);
            GL.Vertex3(1.0f, 1.0f, 0.0f); // top right

            GL.MultiTexCoord2(0, 0.0f, 1.0f);
            GL.Vertex3(0.0f, 1.0f, 0.0f); // top left
        }
        GL.End();
        GL.PopMatrix();
    }
}