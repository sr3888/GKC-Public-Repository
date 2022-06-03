using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MxM;

namespace MxMGameplay
{
    public class GKCVaultDetector : MonoBehaviour
    {
        [SerializeField]
        private VaultDefinition[] m_vaultDefinitions = null;

        [SerializeField]
        private VaultDetectionConfig[] m_vaultConfigurations = null;

        [SerializeField]
        private float m_minStepUpDepth = 1f;

        //[SerializeField]
        //private float m_minStepOffDepth = 1f;

        [SerializeField]
        private LayerMask m_layerMask = new LayerMask();

        [SerializeField]
        private float m_minAdvance = 0.1f; //The minimum advance required to trigger a vault.

        [SerializeField]
        private float m_advanceSmoothing = 10f;

        [SerializeField]
        public float m_maxApproachAngle = 60f;

        private MxMAnimator m_mxmAnimator;
        //private MxMRootMotionApplicator m_playerController;
        private playerController m_playerController;
        private MxMTrajectoryGenerator m_trajectoryGenerator;

        private int m_vaultAnalysisIterations;

        private VaultDetectionConfig m_curConfig;

        private float m_minVaultRise;
        private float m_maxVaultRise;
        private float m_minVaultDepth;
        private float m_maxVaultDepth;
        private float m_minVaultDrop;
        private float m_maxVaultDrop;

        private bool m_isVaulting;

        public float Advance { get; set; }
        public float DesiredAdvance { get; set; }

        public void Awake()
        {
            if (m_vaultConfigurations == null || m_vaultConfigurations.Length == 0)
            {
                Debug.LogError("VaultDetector: Trying to Awake Vault Detector with null or empty vault configurations (m_vaultConfiguration)");
                Destroy(this);
                return;
            }

            if (m_vaultDefinitions == null || m_vaultDefinitions.Length == 0)
            {
                Debug.LogError("VaultDetector: Trying to Awake Vault Detector with null or empty vault definitions (m_vaultDefinitions)");
                Destroy(this);
                return;
            }

            m_mxmAnimator = GetComponentInChildren<MxMAnimator>();

            if (m_mxmAnimator == null)
            {
                Debug.LogError("VaultDetector: Trying to Awake Vault Detector but the MxMAnimator component cannot be found");
                Destroy(this);
                return;
            }

            m_trajectoryGenerator = GetComponentInChildren<MxMTrajectoryGenerator>();

            if (m_trajectoryGenerator == null)
            {
                Debug.LogError("VaultDetector: Trying to Awake Vault Detector but there is no Trajectory component found that implements IMxMTrajectory.");
                Destroy(this);
                return;
            }

            //m_playerController = GetComponentInChildren<MxMRootMotionApplicator>();
            m_playerController = GetComponentInChildren<playerController>();

            m_minVaultRise = float.MaxValue;
            m_maxVaultRise = float.MinValue;
            m_minVaultDepth = float.MaxValue;
            m_maxVaultDepth = float.MinValue;
            m_minVaultDrop = float.MaxValue;
            m_maxVaultDrop = float.MinValue;

            foreach (VaultDefinition vd in m_vaultDefinitions)
            {
                switch (vd.VaultType)
                {
                    case EVaultType.StepUp:
                        {
                            if (vd.MinRise < m_minVaultRise) { m_minVaultRise = vd.MinRise; }
                            if (vd.MaxRise > m_maxVaultRise) { m_maxVaultRise = vd.MaxRise; }
                            if (vd.MinDepth < m_minVaultDepth) { m_minVaultDepth = vd.MinDepth; }
                        }
                        break;
                    case EVaultType.StepOver:
                        {
                            if (vd.MinRise < m_minVaultRise) { m_minVaultRise = vd.MinRise; }
                            if (vd.MaxRise > m_maxVaultRise) { m_maxVaultRise = vd.MaxRise; }
                            if (vd.MinDepth < m_minVaultDepth) { m_minVaultDepth = vd.MinDepth; }
                            if (vd.MaxDepth > m_maxVaultDepth) { m_maxVaultDepth = vd.MaxDepth; }
                        }
                        break;
                    case EVaultType.StepOff:
                        {
                            if (vd.MinDepth < m_minVaultDepth) { m_minVaultDepth = vd.MinDepth; }
                            if (vd.MinDrop < m_minVaultDrop) { m_minVaultDrop = vd.MinDrop; }
                            if (vd.MaxDrop > m_maxVaultDrop) { m_maxVaultDrop = vd.MaxDrop; }
                        }
                        break;
                }
            }

            m_curConfig = m_vaultConfigurations[0];
            DesiredAdvance = Advance = 0f;

            m_vaultAnalysisIterations = (int)(m_maxVaultDepth / m_curConfig.ShapeAnalysisSpacing) + 1;
        }

        public void OnEnable()
        {
            m_isVaulting = false;
        }

        public void Update()
        {
            //Check if we are already vaulting
            if (m_isVaulting)
            {
                HandleCurrentVault();
                return;
            }

            if (!CanVault())
                return;

            //We are going to use the transformposition and forward direction a lot. Caching it here for perf
            Vector3 charPos = transform.position;
            Vector3 charForward = transform.forward;
            float approachAngle = 0f;

            //First we must fire a ray forward from the character, slightly above the minimum vault rise (aka the character controller minimum step
            Vector3 probeStart = new Vector3(charPos.x, charPos.y +
                m_curConfig.DetectProbeRadius + m_minVaultRise,
                charPos.z);

            Ray forwardRay = new Ray(probeStart, charForward);
            RaycastHit forwardRayHit;

            if (Physics.SphereCast(forwardRay, m_curConfig.DetectProbeRadius, out forwardRayHit,
                Advance, m_layerMask, QueryTriggerInteraction.Ignore))
            {
                //If we hit something closer than the current advance, then we shorten the advance since we want the
                //First downward sphere case to hit the edge.
                if (forwardRayHit.distance < Advance)
                    Advance = forwardRayHit.distance;

                Vector3 obstacleOrient = Vector3.ProjectOnPlane(forwardRayHit.normal, Vector3.up) * -1f;

                approachAngle = Vector3.SignedAngle(transform.forward, obstacleOrient, Vector3.up);

                //If we encounter an obstacle but at an angle above our max, we don't want to vault so return here.
                //QUESTION: What if the actual vault point (i.e. at the top of the obstacle is within the correct angle?
                if (Mathf.Abs(approachAngle) > m_maxApproachAngle)
                    return;
            }

            //Next we fire a ray vertically downward from the maximum vault rise to the maximum vault drop
            //NOTE: This does not take into consideration a roof or an overhang
            probeStart = transform.TransformPoint(new Vector3(0f, 0f, Advance));
            probeStart.y += m_maxVaultRise;

            Ray probeRay = new Ray(probeStart, Vector3.down);
            RaycastHit probeHit;
            if (Physics.SphereCast(probeRay, m_curConfig.DetectProbeRadius, out probeHit, m_maxVaultRise + m_maxVaultDrop,
                m_layerMask, QueryTriggerInteraction.Ignore))
            {
                //Too high -> cancel the vault
                if (probeHit.distance < Mathf.Epsilon)
                    return;

                //A 'vault over' or 'vault up' may have been detected if the probe distance is between the minimum and maximum vault rise
                if (probeHit.distance < (m_maxVaultRise - m_minVaultRise))
                {
                    //A vault may have been detected

                    //Check if there is enough height to fit the character
                    if (!CheckCharacterHeightFit(probeHit.point, charForward))
                        return;

                    //Calculate the hit offset. This is the offset on a horizontal 2D plane between the start of the ray and the hit point

                    //Here we conduct a shape analysis of the vaultable object and store that data in a Vaultable Profile
                    VaultableProfile vaultable;
                    VaultShapeAnalysis(in probeHit, out vaultable);

                    if (vaultable.VaultType == EVaultType.Invalid)
                        return;

                    //Check for enough space on top of the object (for a step up)
                    if (vaultable.VaultType == EVaultType.StepUp && vaultable.Depth < m_minStepUpDepth)
                        return;

                    //Check object surface gradient (Assume ok for now)

                    //Select appropriate vault defenition
                    VaultDefinition vaultDef = ComputeBestVault(ref vaultable);

                    if (vaultDef == null)
                        return;

                    float facingAngle = transform.rotation.eulerAngles.y;

                    if (vaultDef.LineUpWithObstacle)
                    {
                        facingAngle += approachAngle;
                    }

                    //Pick contacts
                    vaultDef.EventDefinition.ClearContacts();

                    switch (vaultDef.OffsetMethod_Contact1)
                    {
                        case EVaultContactOffsetMethod.Offset: { vaultable.Contact1 += transform.TransformVector(vaultDef.Offset_Contact1); } break;
                        case EVaultContactOffsetMethod.DepthProportion: { vaultable.Contact1 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact1); } break;
                    }

                    vaultDef.EventDefinition.AddEventContact(vaultable.Contact1, facingAngle);

                    if (vaultable.VaultType == EVaultType.StepOver)
                    {
                        switch (vaultDef.OffsetMethod_Contact2)
                        {
                            case EVaultContactOffsetMethod.Offset: { vaultable.Contact2 += transform.TransformVector(vaultDef.Offset_Contact2); } break;
                            case EVaultContactOffsetMethod.DepthProportion: { vaultable.Contact2 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact2); } break;
                        }

                        vaultDef.EventDefinition.AddEventContact(vaultable.Contact2, facingAngle);
                    }

                    //Trigger event
                    m_mxmAnimator.BeginEvent(vaultDef.EventDefinition);

                    m_mxmAnimator.PostEventTrajectoryMode = EPostEventTrajectoryMode.Pause;

                    if (m_playerController != null)
                        m_playerController.gravityPowerActive = true;

                    if (vaultDef.DisableCollision && m_playerController != null)
                        m_playerController.mainCollider.enabled = false;

                    m_isVaulting = true;
                }
                else //Detect a step off or vault over gap
                {
                    Vector3 flatHitPoint = new Vector3(probeHit.point.x, 0f, probeHit.point.z);
                    Vector3 flatProbePoint = new Vector3(probeStart.x, 0f, probeStart.z);

                    Vector3 dir = flatProbePoint - flatHitPoint; // The direction of the ledge

                    if (dir.sqrMagnitude > (m_curConfig.DetectProbeRadius * m_curConfig.DetectProbeRadius) / 4f)
                    {
                        //A step off may have occured

                        //Shape analysis 
                        Vector2 start2D = new Vector2(probeStart.x, probeStart.z);
                        Vector2 hit2D = new Vector2(probeHit.point.x, probeHit.point.z);

                        float hitOffset = Vector2.Distance(start2D, hit2D);

                        VaultableProfile vaultable;
                        VaultOffShapeAnalysis(in probeHit, out vaultable, hitOffset);

                        if (vaultable.VaultType == EVaultType.Invalid)
                            return;

                        VaultDefinition vaultDef = ComputeBestVault(ref vaultable);

                        if (vaultDef == null)
                            return;

                        float facingAngle = transform.rotation.eulerAngles.y;

                        //Pick contacts
                        vaultDef.EventDefinition.ClearContacts();

                        switch (vaultDef.OffsetMethod_Contact1)
                        {
                            case EVaultContactOffsetMethod.Offset: { vaultable.Contact1 += transform.TransformVector(vaultDef.Offset_Contact1); } break;
                            case EVaultContactOffsetMethod.DepthProportion: { vaultable.Contact1 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact1); } break;
                        }

                        vaultDef.EventDefinition.AddEventContact(vaultable.Contact1, facingAngle);

                        m_mxmAnimator.BeginEvent(vaultDef.EventDefinition);
                        m_mxmAnimator.PostEventTrajectoryMode = EPostEventTrajectoryMode.Pause;

                        if (m_playerController != null)
                            m_playerController.gravityPowerActive = true;

                        if (m_playerController != null)
                            m_playerController.mainCollider.enabled = false;

                        m_isVaulting = true;
                    }
                }
            }
        }

        private void HandleCurrentVault()
        {
            if (!m_playerController.gravityPowerActive == m_mxmAnimator.QueryUserTags(EUserTags.UserTag1))
            {
                m_playerController.gravityPowerActive = m_playerController.gravityPowerActive;
            }

            if (m_playerController.mainCollider.enabled == m_mxmAnimator.QueryUserTags(EUserTags.UserTag2))
            {
                m_playerController.mainCollider.enabled = !m_playerController.mainCollider.enabled;
            }

            if (m_mxmAnimator.IsEventComplete)
            {
                m_isVaulting = false;

                if (m_playerController != null)
                    m_playerController.gravityPowerActive = false;

                if (m_playerController != null)
                    m_playerController.mainCollider.enabled = true;

                Advance = 0f;
            }

            return;
        }

        private bool CanVault()
        {
            //Do not trigger a vault if the character is not grounded
            if (!m_playerController.playerOnGround)
                return false;

            //Check that there is user movement input
            if (!m_trajectoryGenerator.HasMovementInput())
                return false;

            //Check that the angle beteen input and the character facing direction is within an acceptable range to vault
            float inputAngleDelta = Vector3.Angle(transform.forward, m_trajectoryGenerator.LinearInputVector);
            if (inputAngleDelta > 45f)
                return false;

            //Calculate Advance and determine if it higher than the minimum required advance to perform a vault
            DesiredAdvance = (m_mxmAnimator.BodyVelocity * m_curConfig.DetectProbeAdvanceTime).magnitude;
            Advance = Mathf.Lerp(Advance, DesiredAdvance, 1f - Mathf.Exp(-m_advanceSmoothing));
            if (Advance < m_minAdvance)
                return false;

            return true;
        }

        private bool CheckCharacterHeightFit(Vector3 a_fromPoint, Vector3 a_forward)
        {
            float radius = m_playerController.capsule.radius;
            Vector3 fromPosition = a_fromPoint + (a_forward * radius * 2f) + (Vector3.up * radius * 1.1f);

            Ray upRay = new Ray(fromPosition, Vector3.up);
            RaycastHit rayHit;
            if (Physics.Raycast(upRay, out rayHit, m_playerController.capsule.height, m_layerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return true;
        }

        private void VaultOffShapeAnalysis(in RaycastHit a_rayHit, out VaultableProfile a_vaultProfile, float hitOffset)
        {
            a_vaultProfile = new VaultableProfile();

            Vector3 lastPoint = a_rayHit.point;
            bool stepOffStart = false;
            for (int i = 1; i < m_vaultAnalysisIterations; ++i)
            {
                Vector3 start = transform.TransformPoint(Vector3.forward *
                    (Advance + hitOffset + (float)i * m_curConfig.ShapeAnalysisSpacing));
                start.y += m_maxVaultRise;

                Ray ray = new Ray(start, Vector3.down);
                RaycastHit rayHit;

                if (Physics.Raycast(ray, out rayHit, m_maxVaultRise + m_maxVaultDrop, m_layerMask, QueryTriggerInteraction.Ignore))
                {
                    float deltaHeight = rayHit.point.y - lastPoint.y;

                    if (!stepOffStart)
                    {
                        if (deltaHeight < -m_minVaultDrop)
                        {
                            a_vaultProfile.Drop = Mathf.Abs(deltaHeight);
                            a_vaultProfile.Contact1 = rayHit.point;
                            stepOffStart = true;
                        }
                    }
                    else
                    {
                        if (deltaHeight > m_minVaultRise)
                        {
                            a_vaultProfile.Depth = a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;

                            if (a_vaultProfile.Depth > 1f)
                            {
                                a_vaultProfile.Rise = deltaHeight;
                                a_vaultProfile.VaultType = EVaultType.StepOff;
                            }
                            else
                            {
                                a_vaultProfile.VaultType = EVaultType.Invalid;
                            }

                            return;
                        }
                        else if (i == m_vaultAnalysisIterations - 1)
                        {
                            a_vaultProfile.Rise = 0f;
                            a_vaultProfile.Depth = a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;
                            a_vaultProfile.VaultType = EVaultType.StepOff;
                            return;
                        }
                    }
                }
                else
                {
                    a_vaultProfile.Depth = a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;

                    if (a_vaultProfile.Depth > 1f)
                    {
                        a_vaultProfile.Rise = 0f;
                        a_vaultProfile.VaultType = EVaultType.StepOff;
                    }
                    else
                    {
                        a_vaultProfile.VaultType = EVaultType.Invalid;
                    }

                    return;
                }

                lastPoint = rayHit.point;
            }
        }

        //============================================================================================
        /**
        *  @brief This function is called to analyse the shape of a potentialy vaultable object. By 
        *  analysing the shape with raycasts, it's easy to then match the metrics of that shape to a 
        *  number of vaulable definitions with accompanying animations
        *  
        *  @param [in RaycastHit] a_rayHit - The raycast restuls for the ray that hit the vaultable edge
        *  @param [out VaultableProfile] a_vaultProfile - the container for all metrics of the shape analysis
        *  
        *  @Todo: Implement with Jobs and Burst
        *         
        *********************************************************************************************/
        private void VaultShapeAnalysis(in RaycastHit a_rayHit, out VaultableProfile a_vaultProfile)
        {
            a_vaultProfile = new VaultableProfile();

            a_vaultProfile.Contact1 = a_rayHit.point;
            //  a_vaultProfile.Rise = m_maxVaultRise - a_rayHit.distance;

            Vector3 charPos = transform.position;
            Vector3 lastPoint = a_rayHit.point;
            Vector3 highestPoint = lastPoint;
            Vector3 lowestPoint = charPos;

            a_vaultProfile.Rise = a_rayHit.point.y - charPos.y;

            //We need to iterate several times, casting rays downwards to determine the shape of the object in
            //a straight line from the character
            for (int i = 1; i < m_vaultAnalysisIterations; ++i)
            {
                //Each iteration we move the starting point one spacing further
                Vector3 start = a_rayHit.point + transform.TransformVector(Vector3.forward
                    * (float)i * m_curConfig.ShapeAnalysisSpacing);

                start.y += charPos.y + m_maxVaultRise;
                Ray ray = new Ray(start, Vector3.down);
                RaycastHit rayHit;

                if (Physics.Raycast(ray, out rayHit, m_maxVaultRise + m_maxVaultDrop, m_layerMask, QueryTriggerInteraction.Ignore))
                {
                    if (rayHit.point.y > highestPoint.y)
                    {
                        highestPoint = rayHit.point;
                    }
                    else if (rayHit.point.y < lowestPoint.y)
                    {
                        lowestPoint = rayHit.point;
                    }

                    float deltaHeight = rayHit.point.y - lastPoint.y;

                    //If the change in height from one ray to another is greater than the minimum vault drop, then
                    //we may have detected a step over. However! We can only declare a vault over if there is enough 
                    //space for the character on the other side. This is determined by controller width and current velocity
                    if (deltaHeight < -m_minVaultDrop)
                    {
                        //Step Over
                        a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;

                        a_vaultProfile.Drop = a_rayHit.point.y - rayHit.point.y;
                        a_vaultProfile.VaultType = EVaultType.StepOver;
                        a_vaultProfile.Contact2 = rayHit.point;

                        return; //TODO: Remove this return point. The entire vault needs to be analysed before a decision is made in case the character doesn't fit
                    }
                    else if (i == m_vaultAnalysisIterations - 1)
                    {
                        a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;
                        a_vaultProfile.Drop = 0f;
                        a_vaultProfile.VaultType = EVaultType.StepUp;
                    }
                }
                else
                {
                    //Step Over Fall
                    a_vaultProfile.Drop = m_maxVaultDrop;
                    a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;
                    a_vaultProfile.VaultType = EVaultType.StepOverFall;
                    return;
                }

                lastPoint = rayHit.point;
            }
        }

        private VaultDefinition ComputeBestVault(ref VaultableProfile a_vaultable)
        {
            foreach (VaultDefinition vaultDef in m_vaultDefinitions)
            {
                if (vaultDef.VaultType == a_vaultable.VaultType)
                {
                    switch (vaultDef.VaultType)
                    {
                        case EVaultType.StepUp:
                            {
                                if (a_vaultable.Depth < vaultDef.MinDepth)
                                    continue;

                                if (a_vaultable.Rise < vaultDef.MinRise || a_vaultable.Rise > vaultDef.MaxRise)
                                    continue;

                            }
                            break;
                        case EVaultType.StepOver:
                            {
                                if (a_vaultable.Depth < vaultDef.MinDepth || a_vaultable.Depth > vaultDef.MaxDepth)
                                    continue;

                                if (a_vaultable.Rise < vaultDef.MinRise || a_vaultable.Rise > vaultDef.MaxRise)
                                    continue;

                                if (a_vaultable.Drop < vaultDef.MinDrop || a_vaultable.Drop > vaultDef.MaxDrop)
                                    continue;

                            }
                            break;
                        case EVaultType.StepOff:
                            {
                                if (a_vaultable.Depth < vaultDef.MinDepth)
                                    continue;

                                if (a_vaultable.Drop < vaultDef.MinDrop || a_vaultable.Drop > vaultDef.MaxDrop)
                                    continue;

                            }
                            break;
                    }

                    return vaultDef;
                }
            }

            return null;
        }
    }
}