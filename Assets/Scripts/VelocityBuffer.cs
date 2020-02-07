using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera), typeof(Jitter))]
public class VelocityBuffer : MonoBehaviour
{
    private Camera _camera;
    private Jitter jitter;

    public Shader velocityShader;
    private Material velocityTexture;
    private RenderTexture velocityBuffer;

    private Vector4 projectionExtents;
    private Matrix4x4 currentViewMat;
    private Matrix4x4 currentViewProjectionMat;
    private Matrix4x4 previousViewProjectionMat;
    private Matrix4x4 previousViewProjectionMat_NoFlip;
    private bool initParams = false;

    public RenderTexture activeVelocityBuffer { get { return velocityBuffer; } }

    private float nextFrame;

    void Start()
    {
        _camera = Camera.main;
        if(!_camera.enabled)
        {
            _camera.enabled = true;
        }
    }

    void Reset()
    {
        _camera = GetComponent<Camera>();
        jitter = GetComponent<Jitter>();
    }

    void Awake()
    {
        Reset();
    }

    void OnPreRender()
    {
        if ((_camera.depthTextureMode & DepthTextureMode.Depth) == 0)
        {
            _camera.depthTextureMode |= DepthTextureMode.Depth;
        }
    }

    void OnPostRender()
    {
        // assign a render texture to the velocity buffer
        if (velocityBuffer != null)
        {
            RenderTexture.ReleaseTemporary(velocityBuffer);
            velocityBuffer = null;        
        }
        if (velocityBuffer == null)
        {
            velocityBuffer = RenderTexture.GetTemporary(_camera.pixelWidth, _camera.pixelHeight, 16, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Default, 1);
            velocityBuffer.filterMode = FilterMode.Point;
            velocityBuffer.wrapMode = TextureWrapMode.Clamp;
        }

        Matrix4x4 currV = _camera.worldToCameraMatrix;
        Matrix4x4 currP = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, true);
        Matrix4x4 currP_NoFlip = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, false);
        Matrix4x4 prevV = initParams ? currentViewMat : currV;

        initParams = true;
        projectionExtents = _camera.GetProjectionExtents(jitter.currentSample.x, jitter.currentSample.y);
        currentViewMat = currV;
        currentViewProjectionMat = currP * currV;
        previousViewProjectionMat = currP * prevV;
        previousViewProjectionMat_NoFlip = currP_NoFlip * prevV;

        RenderTexture activeRT = RenderTexture.active;
        RenderTexture.active = velocityBuffer;
        
        GL.Clear(true, true, Color.black);

        if (velocityTexture == null)
        {
            velocityTexture = new Material(velocityShader);
        }
        if (velocityTexture != null) 
        {
            velocityTexture.hideFlags = HideFlags.DontSave; // object will not be saved to scene, will not be destroyed on new scene
        }

        // set uniforms
        velocityTexture.SetVector("projectionExtents", projectionExtents);
        velocityTexture.SetMatrix("currentViewMat", currentViewMat);
        velocityTexture.SetMatrix("currentViewProjectMat", currentViewProjectionMat);
        velocityTexture.SetMatrix("previousViewProjectMat", previousViewProjectionMat);
        velocityTexture.SetMatrix("previousViewProjectMat_NoFlip", previousViewProjectionMat_NoFlip);

        velocityTexture.SetPass(0);
        DrawFullscreenQuad();

        RenderTexture.active = activeRT;
    }

    void OnApplicationQuit()
    {
        RenderTexture.ReleaseTemporary(velocityBuffer); // manually release velocity buffer
    }


    /// @brief Renders a full-screen quad
    /// Modified from :-
    /// PlayDeadGames, Lasse Jon Fuglsang Pedersen (31 March, 2017). Temporal Reprojection Anti-Aliasing for Unity 5.0+.
    /// [Accessed 2019]. Available from: "https://github.com/playdeadgames/temporal/blob/master/Assets/Scripts/EffectBase.cs".

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
    /// end of citation
}