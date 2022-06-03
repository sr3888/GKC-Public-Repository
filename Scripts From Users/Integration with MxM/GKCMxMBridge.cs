using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MxM;
using MxMGameplay;

    [RequireComponent(typeof(MxMTrajectoryGenerator))]
    [RequireComponent(typeof(LocomotionSpeedRamp))]
    [RequireComponent(typeof(GKCVaultDetector))]
    [RequireComponent(typeof(MxMTIPExtension))]
    public class GKCMxMBridge : MonoBehaviour
    {
        public void TestOnPoseChange(MxMAnimator.PoseChangeData a_poseChangeData)
        {
            Debug.Log("Pose Id: " + a_poseChangeData.PoseId.ToString() + " Speed Mod: "
                      + a_poseChangeData.SpeedMod.ToString() + " Time Offset: " + a_poseChangeData.TimeOffset.ToString());
        }

        private MxMAnimator m_mxmAnimator;
        private MxMTrajectoryGenerator m_trajectoryGenerator;
        private LocomotionSpeedRamp m_locomotionSpeedRamp;
        private GKCVaultDetector m_vaultDetector;
        private playerController m_charController;

        
        [Header("GKC x MxM Setting")]
        [Space]
        public bool usingGKCAirAction = true;
        public float airBlend = 2f;
        public bool usingGKCSprint = false;
        public float sprintBlend = 2f;
        public bool usingGKCStrafe = false;
        public float strafeBlend = 2f;

        [Header("MxM Event Deffinitions")]
        [Space]
        [SerializeField]
        private MxMEventDefinition m_slideDefinition = null;

        [SerializeField]
        private MxMEventDefinition m_jumpDefinition = null;

        [SerializeField]
        private MxMEventDefinition m_danceDefinition = null;

        [Header("MxM Input Profiles")]
        [SerializeField]
        private MxMInputProfile m_generalLocomotion = null;

        [SerializeField]
        private MxMInputProfile m_strafeLocomotion = null;

        [SerializeField]
        private MxMInputProfile m_sprintLocomotion = null;

        private EState m_curState = EState.General;

        private Vector3 m_lastPosition;
        private Vector3 m_curVelocity;

        private float m_defaultControllerHeight;
        private float m_defaultControllerCenter;

        private bool playerOnAir = false;

        private enum EState
        {
            General,
            Sliding
        }

        Vector3 Vector3Up = Vector3.up;

        // Start is called before the first frame update
        void Start()
        {
            m_mxmAnimator = GetComponentInChildren<MxMAnimator>();
            m_trajectoryGenerator = GetComponentInChildren<MxMTrajectoryGenerator>();
            m_locomotionSpeedRamp = GetComponent<LocomotionSpeedRamp>();
            m_charController = GetComponent<playerController>();
            m_vaultDetector = GetComponent<GKCVaultDetector>();

            m_trajectoryGenerator.InputProfile = m_generalLocomotion;

            playerOnAir = false;

            //override some playerController values
            //Movement Settings
            m_charController.stationaryTurnSpeed = 0;
            m_charController.movingTurnSpeed = 0;

            //Animator Settings
            m_charController.animatorForwardInputLerpSpeed = 0.1f;
            m_charController.animatorTurnInputLerpSpeed = 0.1f;


}

        // Update is called once per frame
        void Update()
        {
             
            if (m_locomotionSpeedRamp != null)
                m_locomotionSpeedRamp.UpdateSpeedRamp();

            Vector3 position = transform.position;
            m_curVelocity = (position - m_lastPosition) / Time.deltaTime;
            m_curVelocity.y = 0f;

            switch (m_curState)
            {
                case EState.General:
                    {
                        UpdateGeneral();
                    }
                    break;
                case EState.Sliding:
                    {
                        stopMxMSliding();
                    }
                    break;
            }

            m_lastPosition = position;
            
        }

        private void UpdateGeneral(){}
    
        //Periodically check on character Ground status
        void FixedUpdate()
        {
            if (usingGKCAirAction)
            {
                if (!m_charController.playerOnGround && m_mxmAnimator.IsEventComplete)
                {
                    if (!m_mxmAnimator.IsEventComplete)
                    {
                        return;
                    }
                    else
                        m_mxmAnimator.BlendInController(airBlend);
                        playerOnAir = true;
                }


                if (playerOnAir == true && m_charController.playerOnGround && m_mxmAnimator.IsEventComplete)
                {
                     if (!m_mxmAnimator.IsEventComplete)
                     {
                         return;
                     }
                     else
                         m_mxmAnimator.BlendOutController(airBlend);
                         playerOnAir = false;
                }
            }
        }

        //Strafe
        //Activate/deactivate MxM Strafe using GKC event
        public void activateMxMStrafeMode()
        {
            if (usingGKCStrafe)
            {
                m_mxmAnimator.BlendInController(strafeBlend);
            }
            else if (!usingGKCStrafe)
            {
                m_mxmAnimator.AddRequiredTag("Strafe");
                m_mxmAnimator.SetCalibrationData("Strafe");
                m_mxmAnimator.SetFavourCurrentPose(true, 0.95f);
                m_locomotionSpeedRamp.ResetFromSprint();
                m_mxmAnimator.AngularErrorWarpRate = 360f;
                m_mxmAnimator.AngularErrorWarpThreshold = 270f;
                m_mxmAnimator.AngularErrorWarpMethod = EAngularErrorWarpMethod.TrajectoryFacing;
                m_trajectoryGenerator.TrajectoryMode = ETrajectoryMoveMode.Strafe;
                m_trajectoryGenerator.InputProfile = m_strafeLocomotion;
                m_mxmAnimator.PastTrajectoryMode = EPastTrajectoryMode.CopyFromCurrentPose;
            }
        }
        public void deactivateMxMStrafeMode()
        {
            if (usingGKCStrafe)
            {
                m_mxmAnimator.BlendOutController(strafeBlend);
            }
            else if (!usingGKCStrafe)
            {
                m_mxmAnimator.RemoveRequiredTag("Strafe");
                m_mxmAnimator.SetFavourCurrentPose(false, 1.0f);
                m_mxmAnimator.SetCalibrationData(0);
                m_mxmAnimator.AngularErrorWarpRate = 60.0f;
                m_mxmAnimator.AngularErrorWarpThreshold = 90f;
                m_mxmAnimator.AngularErrorWarpMethod = EAngularErrorWarpMethod.CurrentHeading;
                m_trajectoryGenerator.TrajectoryMode = ETrajectoryMoveMode.Normal;
                m_trajectoryGenerator.InputProfile = m_generalLocomotion;
                m_mxmAnimator.PastTrajectoryMode = EPastTrajectoryMode.ActualHistory;
            }
        }
        
        //Sprint
        //Activate/deactivate MxM Sprint using GKC event
        public void activateMxMSprint()
        {
            if (usingGKCSprint)
            {
                m_mxmAnimator.BlendInController(sprintBlend);
            }
            else if (!usingGKCSprint)
            {
                m_locomotionSpeedRamp.BeginSprint();
                m_trajectoryGenerator.MaxSpeed = 6.7f;
                m_trajectoryGenerator.PositionBias = 6f;
                m_trajectoryGenerator.DirectionBias = 6f;
                m_mxmAnimator.SetCalibrationData("Sprint");
                m_trajectoryGenerator.InputProfile = m_sprintLocomotion;
            }
        }
        public void deactivateMxMSprint()
        {
            if (usingGKCSprint)
            {
                m_mxmAnimator.BlendOutController(sprintBlend);
            }
            else if (!usingGKCSprint)
            {
                m_locomotionSpeedRamp.ResetFromSprint();
                m_trajectoryGenerator.MaxSpeed = 4.3f;
                m_trajectoryGenerator.PositionBias = 10f;
                m_trajectoryGenerator.DirectionBias = 10f;
                m_mxmAnimator.SetCalibrationData("General");
                m_trajectoryGenerator.InputProfile = m_generalLocomotion;
            }
        }

        //Sliding
        //Activate/deactivate MxM Sliding Event using GKC trigger event
        public void activateMxMSliding()
        {
            m_mxmAnimator.BeginEvent(m_slideDefinition);
            startMxMSliding();
        }
        public void startMxMSliding()
        {
            m_curState = EState.Sliding;
            m_charController.capsule.height = m_charController.capsuleHeightOnCrouch;
            m_charController.capsule.center = Vector3Up * m_charController.capsuleHeightOnCrouch / 2;
            m_vaultDetector.enabled = false;
        }
        public void stopMxMSliding()
        {
            if (m_mxmAnimator.IsEventComplete)
            {
                m_vaultDetector.enabled = true;
                m_curState = EState.General;
                m_charController.capsule.height = m_charController.originalHeight;
                m_charController.capsule.center = Vector3Up * (m_charController.originalHeight * 0.5f);
                
            }
        }
    }
