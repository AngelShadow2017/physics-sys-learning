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
            CollisionManager.instance.TraverseAllListener();
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