using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Camera))]
public class Jitter : MonoBehaviour {

    private Camera _camera;

    private static float[] halton = new float[16 * 2];
    public float jitterScale = 1.0f;
    public Vector4 currentSample = Vector4.zero;
    private int currentIndex = -2;

    private static float haltonSequence(int _index, int _base)
    {
        float f = 1.0f;
        float r = 0.0f;

        while (_index > 0)
        {
            f = f / _base;
            r = r + f * (_index % _base);
            _index = _index / _base;
        }

        return r;
    }

    private static void generateSampleDistribution(float[] haltonArray)
    {
        for(int i = 0, n = haltonArray.Length / 2; i != n; ++i )
        {
            float u = haltonSequence(i + 1, 2) - 0.5f;
            float v = haltonSequence(i + 1, 3) - 0.5f;
            haltonArray[2 * i + 0] = u;
            haltonArray[2 * i + 1] = v;
        }
    }

    // Before camera culls the scene
    void OnPreCull()
    {
        // update jitter
        {
            if (currentIndex == -2)
            {
                currentSample = Vector4.zero;
                currentIndex += 1;

                _camera.projectionMatrix = _camera.GetProjectionMatrix();
            }
            else
            {
                currentIndex += 1;
                currentIndex %= halton.Length / 2;

                /// NOTE the below declaration and assignment of n and i seem redundant, can tidy up fetching of sample with a function later
                Vector2 sample;
                int n = halton.Length / 2;
                int i = currentIndex % n;
                sample.x = jitterScale * halton[2 * i];
                sample.y = jitterScale * halton[2 * i + 1];
                currentSample.z = currentSample.x;
                currentSample.w = currentSample.y;
                currentSample.x = sample.x;
                currentSample.y = sample.y;

                _camera.projectionMatrix = _camera.GetProjectionMatrix(sample.x, sample.y);
            }
        }
    }

    void Awake()
    {
        Reset();
        Clear();
        generateSampleDistribution(halton);
    }

    void Reset()
    {
        _camera = GetComponent<Camera>();
    }

    void Clear()
    {
        _camera.ResetProjectionMatrix();
    }

    void OnDisable()
    {
        Clear();
    }
}
