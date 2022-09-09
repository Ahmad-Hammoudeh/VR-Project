using System;
using System.Collections.Generic;
using UnityEngine;
using radix;

namespace PBDFluid
{

    public class GridHash : IDisposable
    {
        public GPURadixSortParticles _GPUSorter;
        private const int THREADS = 1024;
        private const int READ = 0;
        private const int WRITE = 1;

        public int TotalParticles { get; private set; }

        public Bounds Bounds;
        public bool radix = true;
        public float CellSize { get; private set; }

        public float InvCellSize { get; private set; }

        public int Groups { get; private set; }

        /// <summary>
        /// Contains the particles hash value (x) and
        /// the particles index in its position array (y)
        /// </summary>
        public ComputeBuffer IndexMap { get; private set; }

        /// <summary>
        /// Is a 3D grid representing the hash cells.
        /// Contans where the sorted hash values start (x) 
        /// and end (y) for each cell.
        /// </summary>
        public ComputeBuffer Table { get; private set; }

        private BitonicSort m_sort;

        private ComputeShader m_shader;

        private int m_hashKernel, m_clearKernel, m_mapKernel;

        public GridHash(Bounds bounds, int numParticles, float cellSize,bool isRadix)
        {
            radix = isRadix;
            TotalParticles = numParticles;
            CellSize = cellSize;
            InvCellSize = 1.0f / CellSize;

            Groups = TotalParticles / THREADS;
            if (TotalParticles % THREADS != 0) Groups++;

            Vector3 min, max;
            min = bounds.min;

            max.x = min.x + (float)Math.Ceiling(bounds.size.x / CellSize);
            max.y = min.y + (float)Math.Ceiling(bounds.size.y / CellSize);
            max.z = min.z + (float)Math.Ceiling(bounds.size.z / CellSize);

            Bounds = new Bounds();
            Bounds.SetMinMax(min, max);

            int width = (int)Bounds.size.x;
            int height = (int)Bounds.size.y;
            int depth = (int)Bounds.size.z;

            int size = width * height * depth;

            IndexMap = new ComputeBuffer(TotalParticles, 2 * sizeof(int));
            Table = new ComputeBuffer(size, 2 * sizeof(int));

            if (radix) {
                ComputeShader z_shader = Resources.Load("RadixSortParticle") as ComputeShader;
                _GPUSorter = new GPURadixSortParticles(z_shader, numParticles, 2 * sizeof(int));
            }
            else { m_sort = new BitonicSort(TotalParticles); }

            m_shader = Resources.Load("GridHash") as ComputeShader;
            m_hashKernel = m_shader.FindKernel("HashParticles");
            m_clearKernel = m_shader.FindKernel("ClearTable");
            m_mapKernel = m_shader.FindKernel("MapTable");
        }

        public Bounds WorldBounds
        {
            get
            {
                Vector3 min = Bounds.min;
                Vector3 max = min + Bounds.size * CellSize;

                Bounds bounds = new Bounds();
                bounds.SetMinMax(min, max);

                return bounds;
            }
        }

        public void Dispose()
        {
            if(m_sort != null) m_sort.Dispose();

            if (IndexMap != null)
            {
                IndexMap.Release();
                IndexMap = null;
            }

            if (Table != null)
            {
                Table.Release();
                Table = null;
            }
        }

        public void Process(ComputeBuffer particles)
        {
            if (particles.count != TotalParticles)
                throw new ArgumentException("particles.Length != TotalParticles");

            m_shader.SetInt("NumParticles", TotalParticles);
            m_shader.SetInt("TotalParticles", TotalParticles);
            m_shader.SetFloat("HashScale", InvCellSize);
            m_shader.SetVector("HashSize", Bounds.size);
            m_shader.SetVector("HashTranslate", Bounds.min);

            m_shader.SetBuffer(m_hashKernel, "Particles", particles);
            m_shader.SetBuffer(m_hashKernel, "IndexMap", IndexMap);

            //Assign the particles hash to x and index to y.
            m_shader.Dispatch(m_hashKernel, Groups, 1, 1);

            if (radix)
            {
                _GPUSorter.Init(particles);
                _GPUSorter.Sort();
            }
            else m_sort.Sort(IndexMap);
            /*Vector3[] pos=new Vector3[particles.count];
            Vector2Int[] index = new Vector2Int[TotalParticles];
            particles.GetData(pos);
            for(int i=0;i<pos.Length; i++)
            {
                index[i].y = i;
                index[i].x = Hash(pos[i]);
            }
            IndexMap.SetData(index);*/
            //MapTable();
        }

        public int Hash(Vector3 p)
        {
            p = (p - Bounds.min) * InvCellSize;
            /*Vector3 b=new Vector3(1, 1, 1);
            Vector3 a= Bounds.size - b;
            Vector3 c= new Vector3(0,0,0);
            Vector3Int i = ;*/

            return (int)(p.x + p.y * Bounds.size.x + p.z * Bounds.size.x * Bounds.size.y);
        }
        public void MapTable()
        {

            m_shader.SetBuffer(m_mapKernel, "IndexMap", IndexMap);
            m_shader.SetBuffer(m_mapKernel, "Table", Table);

            //For each hash cell find where the index map
            //start and ends for that hash value.
            m_shader.Dispatch(m_mapKernel, Groups, 1, 1);
            
        }

        /// <summary>
        /// Draws the hash grid for debugging.
        /// </summary>
        public void Clear()
        {
            //First sort by the hash values in x.
            //Uses bitonic sort but any other method will work.
            //m_sort.Sort(IndexMap);

            m_shader.SetInt("TotalParticles", TotalParticles);
            m_shader.SetBuffer(m_clearKernel, "IndexMap", IndexMap);
            m_shader.SetBuffer(m_clearKernel, "Table", Table);


            //Clear the previous tables values as not all
            //locations will be written to when mapped.
            m_shader.Dispatch(m_clearKernel, Groups, 1, 1);
        }

    }
}
