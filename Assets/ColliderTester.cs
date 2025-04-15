using System;
using Core.Algorithm;
using UnityEngine;
using ZeroAs.DOTS.Colliders;
using UnityEngine;
using TrueSync;
using System.Collections.Generic;
using BoxCollider = Core.Algorithm.BoxCollider;

namespace DefaultNamespace
{

    [RequireComponent(typeof(MeshRenderer))]
    public class ColliderTester : MonoBehaviour
    {
        public enum MoveMode { Keyboard, Circular, Horizontal, Custom }

        [Header("Collider Settings")]
        public ColliderType colliderType = ColliderType.Circle;
        public FP radius = 1;
        public TSVector2 boxSize = new TSVector2(2, 2);
        public TSVector2 ovalAxis = new TSVector2(2, 1);
        public TSVector2 doubleCircleOffset = new TSVector2(1, 0);

        [Header("Movement Settings")]
        public MoveMode moveMode = MoveMode.Keyboard;
        public FP moveSpeed = 5;
        public FP rotationSpeed = 90;
        public FP amplitude = 3; // For horizontal/circular motion
        public FP frequency = 1;

        private CollisionController controller;
        private MeshRenderer meshRenderer;
        private TSVector2 startPosition;
        private FP currentRotation;
        private FP timeCounter;

        void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            startPosition = transform.position.ToTSVector2();
            CreateCollider();
        }

        void CreateCollider()
        {
            TSVector2 pos = transform.position.ToTSVector2();
            ColliderBase collider;
            switch (colliderType)
            {
                case ColliderType.Circle:
                    collider = new CircleCollider(radius, pos);
                    break;
                case ColliderType.Oval:
                    collider = new OvalCollider(ovalAxis, pos, 0);
                    break;
                case ColliderType.DoubleCircle:
                    collider = new DoubleCircleCollider(
                        radius, 
                        pos - doubleCircleOffset, 
                        pos + doubleCircleOffset
                    );
                    break;
                case ColliderType.Polygon:
                    collider = new BoxCollider(boxSize, pos, 0);
                    break;
                default:
                    throw new Exception("not initialized");
            }
            if (moveMode == MoveMode.Keyboard)
            {
                collider.colliGroup = CollisionGroup.Default;
            }
            else
            {
                collider.colliGroup = CollisionGroup.Bullet;
            }
            controller = new CollisionController(collider);
            collider.enabled = true;
            if (moveMode == MoveMode.Keyboard)
            {
                controller.AddListener(false, (obj) =>
                {
                    meshRenderer.material.color=Color.red;
                    Debug.Log("collide enter "+obj.collider.uniqueID);
                },(obj) =>
                {
                    //meshRenderer.material.color=Color.red;
                    //Debug.Log(obj.collider.uniqueID);
                },(obj) =>
                {
                    meshRenderer.material.color=Color.green;
                    Debug.Log("collide exit ");
                },CollisionGroup.Bullet);
            }
        }

        void Update()
        {
            /*if (moveMode == MoveMode.Keyboard)
            {
                meshRenderer.material.color=Color.green;
            }*/

            if (Input.GetKey(KeyCode.K))
            {
                currentRotation+=rotationSpeed*Time.deltaTime*Mathf.Deg2Rad;
                foreach (var controllerCollider in controller.Colliders)
                {
                    controllerCollider.SetRotation(currentRotation);
                }
            }

            HandleMovement(Time.deltaTime);
        }

        void HandleMovement(float deltaTime)
        {
            timeCounter += deltaTime * frequency;
            
            switch (moveMode)
            {
                case MoveMode.Keyboard:
                    KeyboardControl(deltaTime);
                    break;
                case MoveMode.Circular:
                    CircularMotion();
                    break;
                case MoveMode.Horizontal:
                    HorizontalMotion();
                    break;
                case MoveMode.Custom:
                    CustomMotion();
                    break;
            }
            gameObject.transform.position = startPosition.ToVector();
            gameObject.transform.position += Vector3.forward*10;
        }

        void KeyboardControl(float deltaTime)
        {
            Vector2 input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            ).normalized;

            TSVector2 movement = input.ToTSVector2() * moveSpeed * deltaTime;
            startPosition += movement;
            controller.SetCenter(startPosition);
        }

        void CircularMotion()
        {
            currentRotation += rotationSpeed * Time.deltaTime;
            TSVector2 offset = new TSVector2(
                TSMath.Cos(currentRotation * Mathf.Deg2Rad) * amplitude,
                TSMath.Sin(currentRotation * Mathf.Deg2Rad) * amplitude
            );
            controller.SetCenter(startPosition + offset);
        }

        void HorizontalMotion()
        {
            FP xOffset = TSMath.Sin(timeCounter) * amplitude;
            controller.SetCenter(startPosition + new TSVector2(xOffset, 0));
        }

        void CustomMotion()
        {
            // 示例：8字形运动
            FP x = TSMath.Sin(timeCounter) * amplitude;
            FP y = TSMath.Sin(2 * timeCounter) * amplitude / 2;
            controller.SetCenter(startPosition + new TSVector2(x, y));
        }



        void OnDestroy()
        {
            if (controller != null)
            {
                controller.Destroy();
            }
        }

        /*private void OnRenderObject()
        {
            foreach (var controllerCollider in controller.Colliders)
            {
                controllerCollider.DebugDisplayColliderShape(meshRenderer.material.color);
            }
        }*/
    }
}