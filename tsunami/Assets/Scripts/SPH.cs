using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SPH : MonoBehaviour
{ 
    /* ==== Constants ==== */
    private const float EPS = 0.0001f;
    private const int MX_PARTICLE = 1000000;
    public int particles_num_limit = 1;
    private readonly Vector3 g = new Vector3(0, -9.82f / 0.004f, 0);


    private const float mass = 0.00020543f;
    private const float rest_density = 600.0f * 0.004f * 0.004f * 0.004f;
    private readonly float pdist = Mathf.Pow(mass / rest_density, 1.0f / 3.0f);
    private const float pradi = 2.0f;

    private const float H = 0.01f / 0.004f; // kernel radius
    private const float H2 = H * H; // h2 = h * h;
    private const float acc_limit = 20000.0f;
    private const float damping = 256.0f;
    private const float bound_repul = 10000.0f;

    private const float Kp = 3f / 0.004f / 0.004f; // Pressure Stiffness
    private const float visc = 0.25f * 0.004f; // Viscosity
    private const float tension = 150.0f; // Surface Tension
    private float dt = 0.004f; // time step

    private readonly float Wpoly6C = 315.0f / 64.0f / Mathf.PI / Mathf.Pow(H, 9);
    private readonly float Grad_WspikeC = 45.0f / Mathf.PI / Mathf.Pow(H, 6);
    private readonly float Lapl_WviscC = 45.0f / Mathf.PI / Mathf.Pow(H, 6);


    float tsunamiMagnitude = 15000f;
    /* ===================== */

    /* ==== Useful Functions ==== */
    float dist(Vector3 A)
    {
        return A.magnitude;
    }
    float dist2(Vector3 A)
    {
        return A.sqrMagnitude;
    }


    float Wpoly6(Vector3 r)
    {
        float r2 = dist2(r);
        if (H2 < r2) return 0f;

        float d = (H2 - r2);
        return Wpoly6C * d * d * d;
    }

    Vector3 Grad_Wspike(Vector3 r)
    {
        float r2 = dist2(r);
        if (H2 < r2) return new Vector3(0f, 0f, 0f);

        float rlen = Mathf.Sqrt(r2);
        float d = (H - rlen);
        return (-1) * Grad_WspikeC * d * d / rlen * r;
    }

    float Lapl_Wvisc(Vector3 r)
    {
        float rlen = dist(r);
        if (H < rlen) return 0f;

        return Lapl_WviscC * (H - rlen);
    }

    float pressure(float density)
    {
        return Kp * (density - rest_density);
    }
    /* ===================== */

    /* ==== Arrays ==== */
    int part_n;
    Vector3[] pos = new Vector3[MX_PARTICLE];
    Vector3[] vel = new Vector3[MX_PARTICLE];
    Vector3[] acc = new Vector3[MX_PARTICLE];
    float[] den = new float[MX_PARTICLE];
    
    float[] bound = new float[3] { 70, 70, 15 };  // from 0, 0, 0
    Dictionary<int, List<int> > grid = new Dictionary<int, List<int>>();
    float[] len = new float[3];
    /* ===================== */


    /* ==== Grid Hashing ==== */
    int prime_p = 3061 , prime_mod = 1000000000 + 7;
    int pp , ppp;
    int hash(float gx , float gy , float gz)
    {
        int tp1 = ((int)gx + 1000) * prime_p;
        int tp2 = (((int)gy + 1000) * pp) % prime_mod;
        int tp3 = (((int)gz + 1000) * ppp) % prime_mod;

        int tp4 = (tp1 + tp2) % prime_mod;
        int res = (tp3 + tp4) % prime_mod;

        return res;
    }
    /* ===================== */

    void add_particles(Vector3 a1, Vector3 a2)
    {
        float d = pdist * 0.84f;
        for (float x = a1.x + EPS; x <= a2.x - EPS; x += d)
            for (float y = a1.y + EPS; y <= a2.y - EPS; y += d)
                for (float z = a1.z + EPS; z <= a2.z - EPS; z += d)
                {
                    if(part_n < particles_num_limit)
                    {
                        pos[part_n] = new Vector3(x, y, z);
                        vel[part_n] = new Vector3(0, 0, 0);
                        part_n++;
                    }
                }
    }

    void construct_grid()
    {
        for (int i = 0; i < part_n; i++)
        {
            int gx = (int)(pos[i][0] / len[0]);
            int gy = (int)(pos[i][1] / len[1]);
            int gz = (int)(pos[i][2] / len[2]);

            var h = hash(gx, gy, gz);
            if(!grid.ContainsKey(h))
            {
                grid.Add(h, new List<int>());
            }
            grid[h].Add(i);
        }
    }

    void initial_scene()
    {
        part_n = 0;
        add_particles(transform.position, 
            new Vector3(transform.position.x + 100, transform.position.y + 20,
            transform.position.z + 50));

        construct_grid();
    }
    
    void calc_density()
    {
        for (int i = 0; i < part_n; i++)
        {
            int gx = (int)(pos[i][0] / len[0]);
            int gy = (int)(pos[i][1] / len[1]);
            int gz = (int)(pos[i][2] / len[2]);

            den[i] = 0;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var h = hash(gx + dx, gy + dy, gz + dz);
                        if (!grid.ContainsKey(h)) continue;

                        foreach (int j in grid[h])
                        {
                            // Assume unit mass
                            Vector3 direc = pos[j] - pos[i];
                            den[i] += mass * Wpoly6(direc);
                        }
                    }
                }
            }
        }

    }

    void obtain_forces()
    {
        for (int i = 0; i < part_n; i++)
        {
            Vector3 f_tsunami = new Vector3(0f, 0f, 0f);
            /*
            if(tsunamiManager.GetComponent<TsunamiMovement>().isTriggered)
            {
                GameObject surface = tsunamiManager.GetComponent<TsunamiMovement>().surface;
                Plane plane = tsunamiManager.GetComponent<TsunamiMovement>().surfacePlane;
                Vector3 projection = plane.ClosestPointOnPlane(pos[i]) + plane.normal;

                if (surface.GetComponent<Renderer>().bounds.Contains(projection))//collision_bounds[j].Contains(projection))
                {
                    Vector3 normal = plane.normal;
                    float xdisp = Mathf.Abs(plane.GetDistanceToPoint(pos[i]));

                    if (xdisp <= pradi)
                    {
                        f_tsunami = tsunamiMagnitude * tsunamiManager.GetComponent<TsunamiMovement>().direction;
                    }
                }
            }
            */
            Vector3 f_out = den[i] * (g + f_tsunami); // Gravitation

            Vector3 f_tens = new Vector3(0, 0, 0); // Surface Tension
            Vector3 f_pres = new Vector3(0, 0, 0); // Pressure Gradient
            Vector3 f_visc = new Vector3(0, 0, 0); // Viscosity

            int gx = (int)(pos[i][0] / len[0]);
            int gy = (int)(pos[i][1] / len[1]);
            int gz = (int)(pos[i][2] / len[2]);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var h = hash(gx + dx, gy + dy, gz + dz);
                        if (!grid.ContainsKey(h)) continue;

                        foreach (int j in grid[h])
                        {
                            // Assume unit mass
                            if (j == i) continue; // When j == i, both grad and laplacian vanish
                             
                            Vector3 direc = pos[i] - pos[j];

                            float ave_P = (pressure(den[i]) + pressure(den[j])) / 2f;
                            f_tens = f_tens - tension * Wpoly6(direc) * den[i] * direc; // Surface Tension
                            f_pres = f_pres - ave_P * mass / den[j] * Grad_Wspike(direc); // Pressure Gradient
                            f_visc = f_visc + (visc * mass / den[j] * Lapl_Wvisc(direc)) * (vel[j] - vel[i]); // Viscosity
                        }
                    }
                }
            }

            acc[i] = (f_out + f_pres + f_visc + f_tens) / den[i];
        }

    }

    /* ==== Collision Arrays ==== */
    public int planes_num = 0;
    public Plane[] planes = new Plane[150];
    public GameObject[] collision_surfaces = new GameObject[150];
    Bounds[] collision_bounds = new Bounds[150];
    public GameObject tsunamiManager;
    /* ===================== */

    void surface_particle_collision(int i)
    {
        // 147
        for (int j = 0; j < planes_num; j++)
        {
            Vector3 projection = planes[j].ClosestPointOnPlane(pos[i]) + planes[j].normal;

            if (collision_surfaces[j].GetComponent<Renderer>().bounds.Contains(projection))//collision_bounds[j].Contains(projection))
            //if(collision_bounds[j].Contains(projection))
            {
                Vector3 normal = planes[j].normal;
                float xdisp = Mathf.Abs(planes[j].GetDistanceToPoint(pos[i]));

                if (xdisp <= pradi)
                {
                    xdisp = pradi - xdisp;

                    acc[i] = acc[i] + bound_repul * xdisp * normal
                    - damping * Vector3.Dot(vel[i], normal) * normal;
                }
            }
        }
    }
    void update_position()
    {
        for (int i = part_n - 1; i >= 0; i--)
        {
            int gx = (int)(pos[i][0] / len[0]);
            int gy = (int)(pos[i][1] / len[1]);
            int gz = (int)(pos[i][2] / len[2]);
            var h = hash(gx, gy, gz);
            grid[h].Clear();

            float accel2 = dist2(acc[i]);
            if (accel2 > acc_limit * acc_limit)
                acc[i] = acc[i] / Mathf.Sqrt(accel2) * acc_limit;

            surface_particle_collision(i);

            vel[i] = vel[i] + acc[i] * dt;
            pos[i] = pos[i] + vel[i] * dt;
        }

        construct_grid();
    }

    public GameObject particle;
    private GameObject[] particles;

    // Start is called before the first frame update
    void Start()
    {
        pp = (prime_p * prime_p);
        ppp = (pp * prime_p) % prime_mod;

        for (int i = 0; i < 3; i++)
            //len[i] = H;
                len[i] = Mathf.Max(bound[i] / 100.0f, H);
        
        initial_scene();

        particles = new GameObject[part_n];
        for (int i = 0;i < part_n; i++)
        {
            particles[i] = Instantiate(particle, pos[i], transform.rotation);
        }

        foreach (UnityEngine.GameObject surface in GameObject.FindGameObjectsWithTag("SPHCollider"))
        { 
            collision_surfaces[planes_num] = surface;
            collision_bounds[planes_num] = surface.GetComponent<Renderer>().bounds;
            planes[planes_num] = new Plane(-1 * surface.transform.forward,
                surface.transform.position + surface.transform.forward);

            planes_num++;
        }
    }

    // Update is called once per frame
    void Update()
    {
        calc_density();
        obtain_forces();
        update_position();
        
        for (int i = 0; i < part_n; i++)
        {
            particles[i].transform.position = pos[i];
        }
    }
}
