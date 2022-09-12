using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PBDFluid;

public class SPH
{
    /* ==== Particle Struct ==== */
    int SIZE_PARTICLEPOSITION = 3 * sizeof(float);
    private struct SPHParticle
    {
        public Vector3 velocity;
        public Vector3 force;
        public Vector3 acc;

        public float pressure;

        public SPHParticle(Vector3 pos)
        {
            velocity = Vector3.zero;
            force = Vector3.zero;
            pressure = 0.0f;
            acc = Vector3.zero;
        }
    }
    int SIZE_SPHPARTICLE = 10 * sizeof(float);
    /* ===================== */

    /* ==== Constants ==== */
    private const float EPS = 0.0001f;
    private readonly Vector3 g = new Vector3(0, -9.82f / 0.004f, 0);

    private const float mass = 0.00020543f;
    private const float rest_density = 600.0f * 0.004f * 0.004f * 0.004f;
    public float particleDensity = 1000.0f;
    private readonly float pdist = Mathf.Pow(mass / rest_density, 1.0f / 3.0f);
    public float particleRadius = 2.0f;

    public float acc_limit = 10000.0f;
    private const float damping = 256.0f;
    private const float bound_repul = 10000.0f;

    private const float Kp = 3f / 0.004f / 0.004f; // Pressure Stiffness
    private const float visc = 0.25f * 0.004f; // Viscosity
    private const float tension = 150.0f; // Surface Tension
    public float dt = 0.004f; // time step

    public const float H = 0.01f / 0.004f;
    public float H2 = H * H;

    private float Wpoly6C = 315.0f / 64.0f / Mathf.PI / Mathf.Pow(H, 9);
    private float Grad_WspikeC = 45.0f / Mathf.PI / Mathf.Pow(H, 6);
    private float Lapl_WviscC = 45.0f / Mathf.PI / Mathf.Pow(H, 6);

    // will be assigned after calculating particle radius
    public float ParticleVolume;

    /* ===================== */

    int kernelComputeDensityPressure;
    int kernelComputeForces;
    int kernelComputeColliders;
   
    // Arrays and buffers
    SPHParticle[] particlesArrayRead;
    SPHParticle[] particlesArrayWrite;

    public ComputeBuffer particlesBufferRead;
    public ComputeBuffer particlesBufferWrite;

    public Vector3[] particlePositionsArrayRead;
    public Vector3[] particlePositionsArrayWrite;
    public ComputeBuffer particlePositionsBufferRead;
    public ComputeBuffer particlePositionsBufferWrite;

    float[] particlesDensityArray;
    public ComputeBuffer particlesDensityBuffer;

    public int[] wrongStepArray = new int[4];
    public ComputeBuffer wrongStepBuffer;
    // =========================

    public GridHash Hash { get; private set; }
    public Bounds boundr;

    public int particlesNumber = 0;

    // assigned by manager
    public Transform boundary;
    public ComputeShader shader;
    int groupSize;


    List<Vector3> positions = new List<Vector3>();
    
    public void initialize(Transform _boundary , int _particlesNumber , ComputeShader _shader)
    {
        boundary = _boundary;
        particlesNumber = _particlesNumber;
        shader = _shader;

        initParticles();
        initShader();
    }
    public void initParticles()
    {
        // Calculate particle radius based on spawn area
        Bounds spawnBounds = GameObject.Find("SpawnArea").GetComponent<Renderer>().bounds;
        Vector3 minBound = spawnBounds.min;
        Vector3 maxBound = spawnBounds.max;

        getMinMax(ref minBound, ref maxBound);
        Bounds bounds = new Bounds();
        bounds.SetMinMax(minBound, maxBound);

        particleRadius = Mathf.Pow((bounds.size.x * bounds.size.y * bounds.size.z) / (float)(particlesNumber), (float)1 / (float)3) / 1.4f;
        // =============

        // create particle positions, update particles number and particle volume
        CreateParticles(particleRadius, particleRadius * 2f * 0.7f, bounds);
        particlesNumber = positions.Count;

        ParticleVolume = (4.0f / 3.0f) * Mathf.PI * Mathf.Pow(particleRadius, 3);


        // calculate groupSize value
        kernelComputeDensityPressure = shader.FindKernel("ComputeDensityPressure");
        uint numThreadsX;
        shader.GetKernelThreadGroupSizes(kernelComputeDensityPressure, out numThreadsX, out _, out _);
        groupSize = Mathf.CeilToInt(particlesNumber / (float)numThreadsX);

        // Initialize particle arrays
        particlesArrayRead = new SPHParticle[particlesNumber];
        particlesArrayWrite = new SPHParticle[particlesNumber];
        particlePositionsArrayRead = new Vector3[particlesNumber];
        particlePositionsArrayWrite = new Vector3[particlesNumber];
        particlesDensityArray = new float[particlesNumber];


        for (int i = 0; i < particlesNumber; i++)
        {
            particlesArrayRead[i] = new SPHParticle();
            particlesArrayWrite[i] = new SPHParticle();

            particlePositionsArrayRead[i] = particlePositionsArrayWrite[i] =
                positions[i];
        }
    }

    private void CreateParticles(float radius, float Spacing,Bounds bounds)
    {
        float HalfSpacing = Spacing / 2f;

    
        int numX = (int)((bounds.size.x + HalfSpacing) / (Spacing));
        int numY = (int)((bounds.size.y + HalfSpacing) / (Spacing));
        int numZ = (int)((bounds.size.z + HalfSpacing) / (Spacing));

        for (int z = 0; z < numZ; z++)
        {
            for (int y = 0; y < numY; y++)
            {
                for (int x = 0; x < numX; x++)
                {
                    Vector3 pos = new Vector3();
                    float rand = Random.Range(-0.1f * radius, 0.1f * radius);
                    pos.x = Spacing * x + bounds.min.x + HalfSpacing + rand;
                    pos.y = Spacing * y + bounds.min.y + HalfSpacing + rand;
                    pos.z = Spacing * z + bounds.min.z + HalfSpacing + rand;

                    positions.Add(pos);
                }
            }
        }

    }

    public void initShader()
    {
        kernelComputeDensityPressure = shader.FindKernel("ComputeDensityPressure");
        kernelComputeForces = shader.FindKernel("ComputeForces");
        kernelComputeColliders = shader.FindKernel("ComputeColliders");


        // get buffers data from arrays
        particlesBufferRead = new ComputeBuffer(particlesArrayRead.Length, SIZE_SPHPARTICLE);
        particlesBufferRead.SetData(particlesArrayRead);

        particlesBufferWrite = new ComputeBuffer(particlesArrayWrite.Length, SIZE_SPHPARTICLE);
        particlesBufferWrite.SetData(particlesArrayWrite);

        particlePositionsBufferRead = new ComputeBuffer(particlesArrayRead.Length, SIZE_PARTICLEPOSITION);
        particlePositionsBufferRead.SetData(particlePositionsArrayRead);

        particlePositionsBufferWrite = new ComputeBuffer(particlesArrayRead.Length, SIZE_PARTICLEPOSITION);
        particlePositionsBufferWrite.SetData(particlePositionsArrayRead);

        particlesDensityBuffer = new ComputeBuffer(particlesArrayRead.Length, sizeof(float));
        particlesDensityBuffer.SetData(particlesDensityArray);

        // ========================

        boundr = new Bounds(boundary.position, boundary.localScale);

        bool isRadixSort = SwitchToggle.toggleValues[SwitchToggle.getIndex("Radix Sort")];
        Hash = new GridHash(boundr, particlesNumber, particleRadius, isRadixSort);

        // SHP CONSTS
        setComputeShaderConsts();


        // assign buffers to shader
        shader.SetBuffer(kernelComputeDensityPressure, "particlesRead", particlesBufferRead);
        shader.SetBuffer(kernelComputeForces, "particlesRead", particlesBufferRead);
        shader.SetBuffer(kernelComputeColliders, "particlesRead", particlesBufferRead);

        shader.SetBuffer(kernelComputeDensityPressure, "particlesWrite", particlesBufferWrite);
        shader.SetBuffer(kernelComputeForces, "particlesWrite", particlesBufferWrite);
        shader.SetBuffer(kernelComputeColliders, "particlesWrite", particlesBufferWrite);

        shader.SetBuffer(kernelComputeDensityPressure, "particlesDensity", particlesDensityBuffer);
        shader.SetBuffer(kernelComputeForces, "particlesDensity", particlesDensityBuffer);
        shader.SetBuffer(kernelComputeColliders, "particlesDensity", particlesDensityBuffer);

        shader.SetBuffer(kernelComputeDensityPressure, "particlePositionsRead", particlePositionsBufferRead);
        shader.SetBuffer(kernelComputeForces, "particlePositionsRead", particlePositionsBufferRead);
        shader.SetBuffer(kernelComputeColliders, "particlePositionsRead", particlePositionsBufferRead);

        shader.SetBuffer(kernelComputeDensityPressure, "particlePositionsWrite", particlePositionsBufferWrite);
        shader.SetBuffer(kernelComputeForces, "particlePositionsWrite", particlePositionsBufferWrite);
        shader.SetBuffer(kernelComputeColliders, "particlePositionsWrite", particlePositionsBufferWrite);

        shader.SetBuffer(kernelComputeDensityPressure, "Table", Hash.Table);
        shader.SetBuffer(kernelComputeForces, "Table", Hash.Table);
        shader.SetBuffer(kernelComputeDensityPressure, "IndexMap", Hash.IndexMap);
        shader.SetBuffer(kernelComputeForces, "IndexMap", Hash.IndexMap);

        // =============
        shader.SetFloat("HashScale", Hash.InvCellSize);
        shader.SetVector("HashSize", Hash.Bounds.size);
        shader.SetVector("HashTranslate", Hash.Bounds.min);

        wrongStepArray[0] = 0;
        wrongStepBuffer = new ComputeBuffer(wrongStepArray.Length, sizeof(int));
        wrongStepBuffer.SetData(wrongStepArray);
        shader.SetBuffer(kernelComputeColliders, "wrongStep", wrongStepBuffer);

        shader.SetInt("particleCount", particlesArrayRead.Length);
    }

    public void iniAdabtiveTimeStep()
    {
        wrongStepArray[0] = 0;
        
        if (wrongStepBuffer != null) wrongStepBuffer.Dispose();

        wrongStepBuffer = new ComputeBuffer(wrongStepArray.Length, sizeof(int));
        wrongStepBuffer.SetData(wrongStepArray);

        shader.SetBuffer(kernelComputeColliders, "wrongStep", wrongStepBuffer);
    }

    public void adaptiveTimeStep()
    {
        wrongStepBuffer.GetData(wrongStepArray);

        if (wrongStepArray[0] == 1)
        {
            if(dt > 0.002f) dt /= 2f;
        }
        else if (wrongStepArray[0] == 0)
        {
            if (dt < 0.004f)
            {
                dt *= 2f;
            }
            swapParticlePositionBuffers();
        }
        //print(dt);
        shader.SetFloat("dt", dt);
    }

    public void Update()
    {
        Hash.Process(particlePositionsBufferRead);
        Hash.MapTable();

        shader.Dispatch(kernelComputeDensityPressure, groupSize, 1, 1);

        shader.Dispatch(kernelComputeForces, groupSize, 1, 1);

        iniAdabtiveTimeStep();
        shader.Dispatch(kernelComputeColliders, groupSize, 1, 1);
        adaptiveTimeStep();
    }

    public void clear()
    {
        Hash.Clear();
    }

    public void OnDestroy()
    {
        Hash.Dispose();
        if (Hash.radix) Hash._GPUSorter.Free();
        particlesBufferRead.Dispose();
        particlesDensityBuffer.Dispose();
        particlePositionsBufferRead.Dispose();
        particlePositionsBufferWrite.Dispose();
    }
    public void swapParticlePositionBuffers()
    {
        ComputeBuffer tmp1 = particlePositionsBufferRead;
        particlePositionsBufferRead = particlePositionsBufferWrite;
        particlePositionsBufferWrite = tmp1;

        ComputeBuffer tmp2 = particlesBufferRead;
        particlesBufferRead = particlesBufferWrite;
        particlesBufferWrite = tmp2;

        shader.SetBuffer(kernelComputeDensityPressure, "particlePositionsRead", particlePositionsBufferRead);
        shader.SetBuffer(kernelComputeForces, "particlePositionsRead", particlePositionsBufferRead);
        shader.SetBuffer(kernelComputeColliders, "particlePositionsRead", particlePositionsBufferRead);

        shader.SetBuffer(kernelComputeDensityPressure, "particlePositionsWrite", particlePositionsBufferWrite);
        shader.SetBuffer(kernelComputeForces, "particlePositionsWrite", particlePositionsBufferWrite);
        shader.SetBuffer(kernelComputeColliders, "particlePositionsWrite", particlePositionsBufferWrite);

        shader.SetBuffer(kernelComputeDensityPressure, "particlesRead", particlesBufferRead);
        shader.SetBuffer(kernelComputeForces, "particlesRead", particlesBufferRead);
        shader.SetBuffer(kernelComputeColliders, "particlesRead", particlesBufferRead);

        shader.SetBuffer(kernelComputeDensityPressure, "particlesWrite", particlesBufferWrite);
        shader.SetBuffer(kernelComputeForces, "particlesWrite", particlesBufferWrite);
        shader.SetBuffer(kernelComputeColliders, "particlesWrite", particlesBufferWrite);
    }

    void setComputeShaderConsts()
    {
        shader.SetFloat("smoothingRadius", H);
        shader.SetFloat("smoothingRadiusSq", H2);

        shader.SetFloat("EPS", EPS);
        shader.SetVector("g", g);

        shader.SetFloat("mass", mass);
        shader.SetFloat("rest_density", rest_density);
        shader.SetFloat("pdist", pdist);
        shader.SetFloat("particleRadius", particleRadius);
        shader.SetFloat("H", H);
        shader.SetFloat("H2", H2);
        shader.SetFloat("acc_limit", acc_limit);
        shader.SetFloat("damping", damping);
        shader.SetFloat("bound_repul", bound_repul);

        shader.SetFloat("Kp", Kp);
        shader.SetFloat("visc", visc);
        shader.SetFloat("tension", tension);
        shader.SetFloat("dt", dt);

        shader.SetFloat("Wpoly6C", Wpoly6C);
        shader.SetFloat("Grad_WspikeC", Grad_WspikeC);
        shader.SetFloat("Lapl_WviscC", Lapl_WviscC);
    }

    void getMinMax(ref Vector3 min, ref Vector3 mx)
    {
        float mnx = Mathf.Min(min.x, mx.x);
        float mny = Mathf.Min(min.y, mx.y);
        float mnz = Mathf.Min(min.z, mx.z);

        float mxx = Mathf.Max(min.x, mx.x);
        float mxy = Mathf.Max(min.y, mx.y);
        float mxz = Mathf.Max(min.z, mx.z);

        min = new Vector3(mnx, mny, mnz);
        mx = new Vector3(mxx, mxy, mxz);

    }
}

/*
public void adaptiveTimeStep()
{
    particlesBufferRead.GetData(particlesArrayRead);
    particlesBufferWrite.GetData(particlesArrayWrite);

    bool yes = false;
    for(int i = 0;i < particlesNumber; i++)
    {
        if(particlesArrayWrite[i].velocity.magnitude*dt > H)
        {
            yes = true;
        }
    }
    if (yes)
    {
        if (dt > 0.0005f)
        {
            dt /= 2f;
        }
    }
    else
    {
        if (dt < 0.004f)
        {
            dt *= 2f;
        }
        swapParticlePositionBuffers();
    }
    print(dt);
    shader.SetFloat("dt", dt);
}*/