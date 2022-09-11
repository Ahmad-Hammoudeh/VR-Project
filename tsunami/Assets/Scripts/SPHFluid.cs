using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PBDFluid;

public class SPHFluid : MonoBehaviour
{

    Pyhsics physicsEngine = new Pyhsics();

    /* ==== Collider Struct ==== */
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
    /* ===================== */

    SPHCollider[] collidersArray;
    ComputeBuffer collidersBuffer;

    ComputeBuffer m_argsBuffer;

    public ComputeShader shader;
    
    public GameObject tsunamiMovement;

    public Transform boundary;

    // Rendering
    public Material material;
    public Material r_material;
    public Material m_volumeMat;
    public Mesh particleMesh = null;
    private RenderVolume m_volume;
    uint[] argsArray = { 0, 0, 0, 0, 0 };
    ComputeBuffer argsBuffer;
    Kernel waterKernel;
    FluidBody body;
    // ===================

    int kernelComputeDensityPressure;
    int kernelComputeForces;
    int kernelComputeColliders;

    int groupSize;

    private bool renderParticles = true;
    private bool renderWater = false;

    void ini_groupSize_Variable()
    {
        kernelComputeDensityPressure = shader.FindKernel("ComputeDensityPressure");
        uint numThreadsX;
        shader.GetKernelThreadGroupSizes(kernelComputeDensityPressure, out numThreadsX, out _, out _);
        groupSize = Mathf.CeilToInt(physicsEngine.particlesNumber / (float)numThreadsX);
    }

    void iniPhysicsEngine()
    {
        physicsEngine.boundary = boundary;
        physicsEngine.particlesNumber = SliderUI.slidersValues[SliderUI.getIndex("Particles")];
        physicsEngine.shader = shader;

        physicsEngine.initParticles();
    }
    


    private void Start()
    {        
        iniPhysicsEngine();
        ini_groupSize_Variable();

        //waterKernel = new Kernel(physicsEngine.particleRadius, physicsEngine.particleRadius * physicsEngine.particleRadius, 29.296875f * Mathf.Pow(particleRadius , 10f)); // 15000.0f / 1.0f);
        waterKernel = new Kernel(physicsEngine.particleRadius, physicsEngine.particleRadius * physicsEngine.particleRadius
            , 7680000f / Mathf.Pow(physicsEngine.particleRadius, 9));

        body = new FluidBody(physicsEngine.particleDensity, physicsEngine.particlesNumber, physicsEngine.ParticleVolume);

        physicsEngine.initShader();
        InitShader();
    }

    void FetchColliders()
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
        shader.SetBuffer(kernelComputeColliders, "colliders", collidersBuffer);
        shader.SetBuffer(kernelComputeForces, "colliders", collidersBuffer);

    }

    private void Update()
    {
        FetchColliders();
        physicsEngine.Hash.Process(physicsEngine.particlePositionsBufferRead);
        physicsEngine.Hash.MapTable();

        shader.Dispatch(kernelComputeDensityPressure, groupSize, 1, 1);

        shader.Dispatch(kernelComputeForces, groupSize, 1, 1);
        
        physicsEngine.iniAdabtiveTimeStep();
        shader.Dispatch(kernelComputeColliders, groupSize, 1, 1);
        physicsEngine.adaptiveTimeStep();


        // Rendering
        renderWater = SwitchToggle.toggleValues[SwitchToggle.getIndex("Render Water")];
        renderParticles = SwitchToggle.toggleValues[SwitchToggle.getIndex("Render Particles")];

        if (renderWater)
        {
            m_volume.FillVolume(body, physicsEngine.Hash, waterKernel, physicsEngine.particlePositionsBufferRead, physicsEngine.particlesDensityBuffer);
            m_volume.Hide = false;
        }
        else m_volume.Hide = true;

        if (renderParticles)
        {
            bool isRadixSort = SwitchToggle.toggleValues[SwitchToggle.getIndex("Radix Sort")];
            if (isRadixSort)
            {
                Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 0);
                r_material.SetBuffer("particles", physicsEngine.particlePositionsBufferRead);
                Graphics.DrawMeshInstancedIndirect(particleMesh, 0, r_material, bounds, argsBuffer);
            }
            else
            {
                if (m_argsBuffer == null)
                {
                    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                    args[0] = particleMesh.GetIndexCount(0);
                    args[1] = (uint)physicsEngine.particlesNumber;

                    m_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                    m_argsBuffer.SetData(args);
                    material.SetBuffer("positions", physicsEngine.particlePositionsBufferRead);
                    material.SetColor("color", Color.cyan);
                    material.SetFloat("diameter", physicsEngine.particleRadius);
                }


                ShadowCastingMode castShadow = ShadowCastingMode.Off;
                bool recieveShadow = false;

                Graphics.DrawMeshInstancedIndirect(particleMesh, 0, material, physicsEngine.boundr, m_argsBuffer, 0, null, castShadow, recieveShadow, 0, Camera.main);
            }
        }
        
        //m_volume.Hide = true;
        physicsEngine.Hash.Clear();

    }
    
    void InitShader()
    {
        kernelComputeDensityPressure = shader.FindKernel("ComputeDensityPressure");
        kernelComputeForces = shader.FindKernel("ComputeForces");
        kernelComputeColliders = shader.FindKernel("ComputeColliders");

        // Tsunami 
        shader.SetInt("tsunamiSurfaceIndex", 0);
       
        // =========================
        // Collider class
        FetchColliders();
        shader.SetInt("colliderCount", collidersArray.Length);
        shader.SetBuffer(kernelComputeColliders, "colliders", collidersBuffer);
        shader.SetBuffer(kernelComputeForces, "colliders", collidersBuffer);


        // Manager
        shader.SetInt("particleCount", physicsEngine.particlesNumber);


        // Rendering
        argsArray[0] = particleMesh.GetIndexCount(0);
        argsArray[1] = (uint)physicsEngine.particlesNumber;
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(argsArray);
        r_material.SetBuffer("particles",physicsEngine.particlePositionsBufferRead);
        r_material.SetFloat("_Radius", physicsEngine.particleRadius);

        m_volume = new RenderVolume(physicsEngine.boundr, physicsEngine.particleRadius);
        m_volume.CreateMesh(m_volumeMat);
    }

     private void OnDestroy()
    {
        physicsEngine.Hash.Dispose();
        if(physicsEngine.Hash.radix)physicsEngine.Hash._GPUSorter.Free();
        physicsEngine.particlesBufferRead.Dispose();
        physicsEngine.particlesDensityBuffer.Dispose();
        physicsEngine.particlePositionsBufferRead.Dispose();
        physicsEngine.particlePositionsBufferWrite.Dispose();
        collidersBuffer.Dispose();
        argsBuffer.Dispose();
    }
}
