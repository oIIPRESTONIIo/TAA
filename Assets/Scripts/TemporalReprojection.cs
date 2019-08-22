using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Jitter), typeof(VelocityBuffer))]
public class TemporalReprojection : MonoBehaviour
{
    private static RenderBuffer[] renderTargets = new RenderBuffer[2];

    private Jitter jitter;
    private VelocityBuffer velocityBuffer;

    public Shader reprojectionShader;
    private Material reprojectionMaterial;
    private RenderTexture[] reprojectionBuffer = new RenderTexture[2];
    private int reprojectionIndex = -1;

    [Range(0.0f, 1.0f)] public float blendWeightMin = 0.85f;
    [Range(0.0f, 1.0f)] public float blendWeightMax = 0.95f;

    public float motionBlurStrength = 1.0f;

    void Reset()
    {
        jitter = GetComponent<Jitter>();
        velocityBuffer = GetComponent<VelocityBuffer>();
    }

    void Clear()
    {
        reprojectionIndex = -1;
    }

    void Awake()
    {
        Reset();
        Clear();
    }

    void Resolve(RenderTexture source, RenderTexture target)
    {
        //EnsureArray(ref reprojectionBuffer, 2);

        if (reprojectionMaterial == null)
        {
            reprojectionMaterial = new Material(reprojectionShader);
        }
        else
        {
            reprojectionMaterial.hideFlags = HideFlags.DontSave; // object will not be saved to scene, will not be destroyed on new scene
        }

        if (reprojectionMaterial == null)
        {
            Graphics.Blit(source, target);
            return;
        }

        // initialise the reprojection buffer
        if (reprojectionBuffer[0] == null)
        {
            reprojectionBuffer[0] = RenderTexture.GetTemporary(source.width, source.height, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            reprojectionBuffer[0].filterMode = FilterMode.Point;
            reprojectionBuffer[0].wrapMode = TextureWrapMode.Clamp;
        }
        if (reprojectionBuffer[1] == null)
        {
            reprojectionBuffer[1] = RenderTexture.GetTemporary(source.width, source.height, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            reprojectionBuffer[1].filterMode = FilterMode.Point;
            reprojectionBuffer[1].wrapMode = TextureWrapMode.Clamp;
        }

        if (reprojectionIndex == -1)
        {
            reprojectionIndex = 0;
            reprojectionBuffer[reprojectionIndex].DiscardContents();
            Graphics.Blit(source, reprojectionBuffer[reprojectionIndex]);
        }

        int indexRead = reprojectionIndex;
        // ensure the write index is the opposite of the read
        int indexWrite = (reprojectionIndex + 1) % 2;

        Vector4 currentJitter = jitter.currentSample;
        currentJitter.x /= source.width;
        currentJitter.z /= source.width;
        currentJitter.y /= source.height;
        currentJitter.w /= source.height;

        reprojectionMaterial.SetTexture("mainTexture", source);
        reprojectionMaterial.SetTexture("historyTexture", reprojectionBuffer[indexRead]);
        reprojectionMaterial.SetTexture("velocityBuffer", velocityBuffer.activeVelocityBuffer);
        reprojectionMaterial.SetVector("jitter", currentJitter);
        reprojectionMaterial.SetFloat("blendWeightMin", blendWeightMin);
        reprojectionMaterial.SetFloat("blendWeightMax", blendWeightMax);
        reprojectionMaterial.SetFloat("motionBlurStrength", motionBlurStrength);

        // reproject frame n-1 into output + history buffer
        {
            renderTargets[0] = reprojectionBuffer[indexWrite].colorBuffer;
            renderTargets[1] = target.colorBuffer;

            Graphics.SetRenderTarget(renderTargets, source.depthBuffer);
            reprojectionMaterial.SetPass(0);
            reprojectionBuffer[indexWrite].DiscardContents();

            DrawFullscreenQuad();

            reprojectionIndex = indexWrite;
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture target)
    {
        if (target != null)// resolve without additional blit when not end of chain
        {
            Resolve(source, target);
        }
        else
        {
            // Initialise target Render Texture
            RenderTexture tempTarget = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, source.antiAliasing);
            {
                // render to tempTarget render texture
                Resolve(source, tempTarget);
                // copy to target render Texture
                Graphics.Blit(tempTarget, target);
            }
            // free tempTarget memory
            RenderTexture.ReleaseTemporary(tempTarget);
        }
    }

    void OnApplicationQuit()
    {
        RenderTexture.ReleaseTemporary(reprojectionBuffer[0]);
        RenderTexture.ReleaseTemporary(reprojectionBuffer[1]);
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