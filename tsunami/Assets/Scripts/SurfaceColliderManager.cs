using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurfaceColliderManager
{
    private struct SPHCollider
    {
        public Vector3 position;
        public Vector3 minBound;
        public Vector3 maxBound;
        public Vector3 normal;
        public Vector3 right;
        public Vector3 up;
        public Vector2 scale;
        public SPHCollider(GameObject surface)
        {
            position = surface.transform.position;// + surface.transform.forward;

            minBound = surface.GetComponent<Renderer>().bounds.min;// + surface.transform.forward;
            maxBound = surface.GetComponent<Renderer>().bounds.max;// + surface.transform.forward;
            normal = -1 * surface.transform.forward;
            right = surface.transform.right;
            up = surface.transform.up;
            scale = new Vector2(surface.transform.lossyScale.x / 2f, surface.transform.lossyScale.y / 2f);
        }
    }
    int SIZE_SPHCOLLIDER = 20 * sizeof(float);

    SPHCollider[] collidersArray;
    ComputeBuffer collidersBuffer;

    public ComputeShader shader;
    int kernelComputeColliders;

    public void fetchColliders()
    {
        // Get colliders
        GameObject[] collidersGO = GameObject.FindGameObjectsWithTag("SPHCollider");
        if (collidersArray == null || collidersArray.Length != collidersGO.Length)
        {
            collidersArray = new SPHCollider[collidersGO.Length];
            if (collidersBuffer != null)
            {
                collidersBuffer.Dispose();
            }
            collidersBuffer = new ComputeBuffer(collidersArray.Length, SIZE_SPHCOLLIDER);
        }

        for (int i = 0; i < collidersArray.Length; i++)
            collidersArray[i] = new SPHCollider(collidersGO[i]);

        collidersBuffer.SetData(collidersArray);

        kernelComputeColliders = shader.FindKernel("ComputeColliders");

        shader.SetBuffer(kernelComputeColliders, "colliders", collidersBuffer);

        shader.SetInt("colliderCount", collidersArray.Length);
    }

    public void OnDestroy()
    {
        collidersBuffer.Dispose();
    }
}
