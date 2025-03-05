/*
 * QuakeCharacterController.cs
 * Liam Rousselle [2025]
 * 
 * Inspired by the movement mechanics in Quake (1996) by id Software
 * Movement logic was analyzed from Quake but reimplemented from scratch in C# for Unity.
 * Original Quake engine source is licensed under GPLv2: https://github.com/id-Software/Quake
 */

using UnityEngine;

namespace QuakeLR
{
    [RequireComponent(typeof(CharacterController))]
    public class QuakeCharacterController : MonoBehaviour
    {
        [Header("Force Settings")] 
        [SerializeField]
        [Tooltip("The speed which the controller will travel at")]
        private float MaxWalkSpeed = 11.4f; //320.0f in Quake Units

        [SerializeField] 
        [Tooltip("The force applied upward when the controller is requested to jump.")]
        private float JumpPower = 9.64f; //270.0f in Quake Units
        
        [SerializeField] 
        [Tooltip("The friction applied to the character controller.")]
        private float Friction = 4.0f;

        [SerializeField] 
        [Tooltip("How quickly the controller will accelerate downward when in falling state")]
        private float Gravity = 28.6f; //800.0f m/s^2 in Quake Units
        
        [Header("Acceleration Settings")]
        [SerializeField] 
        [Tooltip("How quickly the controller will build up speed to the MaxWalkSpeed")]
        private float AccelerationSpeed = 10.0f;
        
        [SerializeField] 
        [Tooltip("How quickly the controller will come to a stop after accelerating.")]
        private float StopSpeed = 3.57f; // 100.0f in Quake Units

        [Header("Collision")] 
        [SerializeField] 
        [Tooltip("A mask of all objects which the player can land on/move on")]
        private LayerMask GroundMask;
        
        private CharacterController m_CharacterController = null;

        private Vector3 m_WishMoveDirection = Vector3.zero;
        private Vector3 m_Velocity = Vector3.zero;
        
        private bool m_OnGround = true;
        private bool m_RememberJump = false;
        
        private static readonly Vector3 k_XZPlane = new Vector3(1.0f, 0.0f, 1.0f);
        
        /**
         * @note worldMoveDirection SHOULD NOT BE affected by deltaTime in any way as this method serves as a setter function
         * 
         * Serves the purpose to pass the worldMoveDirection to m_WishMoveDirection.
         * @param worldMoveDirection: The unit direction relative to the world which the controller wishes to move in
         */
        public void Move(Vector3 worldMoveDirection)
        {
            m_WishMoveDirection = worldMoveDirection * MaxWalkSpeed;
        }

        /**
         * Do not call this function directly! (unless you know what you're doing)
         * This will launch the character up in the air WITHOUT CHECKING IF THE PLAYER IS GROUNDED
         */
        public void Jump()
        {
            m_RememberJump = true;
        }

        /**
         * Will make the controller jump however there are safe guards implemented.
         * @note Only safe guard currently implemented is to check if the character is grounded.
         */
        public void TryJump()
        {
            if (m_OnGround)
                Jump();
        }
        
        /**
         * This should be called every frame by the MovementController referencing this component.
         * @note Controller think will not use Time.deltaTime just in-case of any mishaps by other users implementing this movement system
         */
        public void ControllerThink(float deltaTime)
        {
            deltaTime = Mathf.Min(deltaTime, 1.0f); //Just in-case a massive frame drop happens during runtime
            
            EvaluateOnGround();
            UserGravity(deltaTime);
            UserFriction(deltaTime);
            
            if (m_OnGround)
                GroundAccelerate(deltaTime);
            else
                AirAccelerate(deltaTime);

            if (m_CharacterController)
                m_CharacterController.Move(m_Velocity * deltaTime);
            else
                Debug.LogWarning("Missing reference to m_CharacterController (typeof CharacterController)");
        }

        /**
         * Used to set the state of m_OnGround.
         *
         * @note Called at the start of every "ControllerThink" tick
         */
        private void EvaluateOnGround()
        {
            float bodyRadius = m_CharacterController.radius;
            float bodyHalfHeight = m_CharacterController.height * 0.5f;

            Vector3 origin = transform.position;
            Vector3 sphereCheckPosition = origin - (transform.up * bodyHalfHeight - transform.up * bodyRadius * 0.5f);
            
            m_OnGround = Physics.CheckSphere(sphereCheckPosition, bodyRadius, GroundMask.value);
        }

        /**
         * Responsible for handling all gravity related physics.
         * @note Jumping is handled here
         * @note Called every "ControllerThink" tick.
         */
        private void UserGravity(float deltaTime)
        {
            if (m_OnGround)
            {
                if (m_RememberJump)
                {
                    m_Velocity.y = JumpPower;
                    m_OnGround = false;

                    //hack because the character controller won't jump after getting stuck on a slope
                    m_CharacterController.Move(Vector3.up * JumpPower * deltaTime);
                    m_Velocity.y -= JumpPower * deltaTime;
                }
                else
                {
                    m_Velocity.y = 0.0f;
                }
            }
            else
            {
                m_Velocity.y -= deltaTime * Gravity;
            }

            m_RememberJump = false; //Reset jump input for next frame
        }
        
        /**
         * Inspiration taken from here: "https://github.com/id-Software/Quake/blob/master/WinQuake/sv_user.c#L122"
         * 
         * UserFriction serves the purpose of de-accelerating the movement velocity. Without it, it would be
         * impossible for the user to stop.
         *
         * @note Also prevents awkward acceleration in movement.
         */
        private void UserFriction(float deltaTime)
        {
            float speed = Vector3.Scale(k_XZPlane, m_Velocity).magnitude;
            if (speed < 0.01f)
            {
                m_Velocity = Vector3.Scale(m_Velocity, Vector3.up);
                return;
            }

            float drop = 0.0f;
            if (m_OnGround)
            {
                float control = speed < StopSpeed ? StopSpeed : speed;
                drop += control * Friction * deltaTime;
            }

            float newSpeed = Mathf.Max(speed - drop, 0.0f) / speed;
            m_Velocity *= newSpeed;
        }

        /**
         * Inspiration taken from here: "https://github.com/id-Software/Quake/blob/master/WinQuake/sv_user.c#L170"
         * 
         * Serves the purpose of moving the Character Controller while on the ground.
         * Does not directly move the Character Controller here, instead, it affects the velocity which
         * will then be the direction which the Character Controller is moved in the "ClientThink" function.
         *
         * @note Only called while m_OnGround is true.
         * @note Called every "ClientThink" tick.
         */
        private void GroundAccelerate(float deltaTime)
        {
            float alignment = Vector3.Dot(m_Velocity, m_WishMoveDirection.normalized);
            float addSpeed = MaxWalkSpeed - alignment;
            if (addSpeed <= 0)
                return;

            float accelSpeed = Mathf.Min(AccelerationSpeed * deltaTime * MaxWalkSpeed, addSpeed);
            m_Velocity += accelSpeed * Vector3.Scale(m_WishMoveDirection.normalized, k_XZPlane);
        }

        /**
         * Inspiration taken from here: "https://github.com/id-Software/Quake/blob/master/WinQuake/sv_user.c#L207"
         *
         * A brief description of how air strafing occurs:
         *      Air-strafing is a Quake movement technique which allows for rapid acceleration in air.
         *      The achieve this by holding a strafe key then turning there character controller (via their mouse)
         *      to rotate towards that direction. This is a bug which causes the character to accelerate in that direction.
         *      In the code, the "alignment" variable plays a crucial role in air-strafing. The dot product is the
         *      alignment of two unit vectors (essentially how much they match one another)
         *      If the dot product returns a negative value it means they are look away, while as if it returns
         *      a positive one it means they are facing similar directions. "addSpeed" is set to "wishSpeed" subtracted
         *      by "alignment". However, since "m_Velocity" is NOT a unit vector. This means it's size isn't 1. This
         *      skews addSpeed, and when alignment is negative it rapidly accelerates the speed to add.
         * 
         * @note Only called while m_OnGround is false.
         * @note Called every "ClientThink" tick.
         */
        private void AirAccelerate(float deltaTime)
        {
            float wishSpeed = Mathf.Min(m_WishMoveDirection.magnitude, 1.07f); //1.07f is 30.0f in Quake Units
            float alignment = Vector3.Dot(m_Velocity, m_WishMoveDirection.normalized);

            float addSpeed = wishSpeed - alignment;
            if (addSpeed <= 0.0f)
                return;

            float accelSpeed = Mathf.Min(AccelerationSpeed * wishSpeed * deltaTime, addSpeed);
            m_Velocity += m_WishMoveDirection * accelSpeed;
        }

        private void Awake()
        {
            m_CharacterController = GetComponent<CharacterController>();
        }
    }
}