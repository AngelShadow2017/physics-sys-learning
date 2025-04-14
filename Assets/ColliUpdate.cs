using System;
using Core.Algorithm;
using UnityEngine;

namespace DefaultNamespace
{
    public class ColliUpdate:MonoBehaviour
    {
        void Awake()
        {
            this.transform.position=Vector3.zero;
            CollisionManager.instance = new CollisionManager();
        }

        private void Update()
        {
            //CollisionManager.instance.ReloadJobCollisionChecker();
            CollisionManager.instance.TraverseAllListener();
            if (
                CollisionManager.instance.converterManager.IsCreated)
            {
                
                CollisionManager.instance.converterManager.Dispose();
            }
        }

        private void LateUpdate()
        {
            CollisionManager.instance.nativeCollisionManager.CompactVertexBuffers();
        }

        private void OnPostRender()
        {
            CollisionManager.instance.DebugDisplayShape(transform.localToWorldMatrix);
        }

        private void OnDestroy()
        {
            CollisionManager.instance.Dispose();
        }
    }
}