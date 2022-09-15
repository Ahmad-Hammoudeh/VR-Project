using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PBDFluid;

public class Manager : MonoBehaviour
{

    SPH SPHManager = new SPH();
    SurfaceColliderManager surfaceColliderManager = new SurfaceColliderManager();

    ComputeBuffer m_argsBuffer;

    public ComputeShader shader;
    
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

    private bool renderParticles = true;
    private bool renderWater = false;

    private void Start()
    {
        SPHManager.initialize(boundary , 
            SliderUI.slidersValues[SliderUI.getIndex("Particles")] ,
            shader);

        // initialize Water Rendering
        waterKernel = new Kernel(SPHManager.particleRadius, SPHManager.particleRadius * SPHManager.particleRadius
            , 7680000f / Mathf.Pow(SPHManager.particleRadius, 9));

        body = new FluidBody(SPHManager.particleDensity, SPHManager.particlesNumber, SPHManager.ParticleVolume);
        // =============

        // intialize surface colliders
        surfaceColliderManager.shader = shader;
        surfaceColliderManager.fetchColliders();
        // =============

        initShader();
    }

    private void Update()
    {
        SPHManager.Update();

        rendering();

        SPHManager.clear();
    }
    
    void rendering()
    {
        renderWater = SwitchToggle.toggleValues[SwitchToggle.getIndex("Render Water")];
        renderParticles = SwitchToggle.toggleValues[SwitchToggle.getIndex("Render Particles")];

        if (renderWater)
        {
            m_volume.FillVolume(body, SPHManager.Hash, waterKernel, SPHManager.particlePositionsBufferRead, SPHManager.particlesDensityBuffer);
            m_volume.Hide = false;
        }
        else m_volume.Hide = true;

        if (renderParticles)
        {
            bool isRadixSort = SwitchToggle.toggleValues[SwitchToggle.getIndex("Radix Sort")];
            if (isRadixSort)
            {
                Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 0);
                r_material.SetBuffer("particles", SPHManager.particlePositionsBufferRead);
                Graphics.DrawMeshInstancedIndirect(particleMesh, 0, r_material, bounds, argsBuffer);
            }
            else
            {
                if (m_argsBuffer == null)
                {
                    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                    args[0] = particleMesh.GetIndexCount(0);
                    args[1] = (uint)SPHManager.particlesNumber;

                    m_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                    m_argsBuffer.SetData(args);
                    material.SetBuffer("positions", SPHManager.particlePositionsBufferRead);
                    material.SetColor("color", Color.cyan);
                    material.SetFloat("diameter", SPHManager.particleRadius);
                }


                ShadowCastingMode castShadow = ShadowCastingMode.Off;
                bool recieveShadow = false;

                Graphics.DrawMeshInstancedIndirect(particleMesh, 0, material, SPHManager.boundr, m_argsBuffer, 0, null, castShadow, recieveShadow, 0, Camera.main);
            }
        }
    }
    void initShader()
    {
        // Rendering
        argsArray[0] = particleMesh.GetIndexCount(0);
        argsArray[1] = (uint)SPHManager.particlesNumber;
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(argsArray);
        r_material.SetBuffer("particles",SPHManager.particlePositionsBufferRead);
        r_material.SetFloat("_Radius", SPHManager.particleRadius);

        m_volume = new RenderVolume(SPHManager.boundr, SPHManager.particleRadius);
        m_volume.CreateMesh(m_volumeMat);
    }

     private void OnDestroy()
    {
        SPHManager.OnDestroy();
        surfaceColliderManager.OnDestroy();
        argsBuffer.Dispose();
    }
}
