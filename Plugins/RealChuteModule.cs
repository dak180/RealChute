using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


/*RealChute was made by stupid_chris and is licensed under CC-BY-NC-SA. You can remix, modify and redistribute
 * the work, but you must give attribution to the original author and you cannot sell your derivatives.
 * For more informtion contact me on the forum.*/


namespace stupid_chris
{
    //------------------------- Part Module -------------------------
    public class RealChuteModule : PartModule
    {
        // Values from the .cfg file
        [KSPField]
        public float preDeployedDrag;
        [KSPField]
        public float deployedDrag;
        [KSPField]
        public float stowedDrag;
        [KSPField]
        public float minDeploymentAlt;
        [KSPField]
        public float deploymentAlt;
        [KSPField]
        public float preDeploymentSpeed;
        [KSPField]
        public float deploymentSpeed;
        [KSPField]
        public string preDeploymentAnimation;
        [KSPField]
        public string deploymentAnimation;
        [KSPField]
        public string parachuteName;
        [KSPField]
        public string capName;
        [KSPField]
        public float spareChutes;

        //Persistant values
        [KSPField]
        public float cutSpeed;
        [KSPField(isPersistant = true)]
        public bool initiated = false;
        [KSPField(isPersistant = true)]
        public bool capOff;
        [KSPField(isPersistant = true, guiActive = true, guiName = "Spare chutes")]
        public float chuteCount;

        //Variables
        public Vector3 dragVector;
        public Vector3 up;
        public Vector3 CoM;
        public Quaternion parachuteRotation;
        public Transform parachute;
        public Transform cap;
        public float altitude;
        public float ASL;
        public RaycastHit surface;
        public string deploymentState;
        public bool setCount = false;
        public float debutTime;
        public bool timeSet = false;
        public float currentTime;
        public float deltaTime;
        public float drag;
        public float deploymentTime;
        public string infoList = String.Empty;

        //------------------------- Animations -------------------------
        public void InitiateAnimation(string animationName)
        {
            //Initiates the default values for animations
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];
                animationState.normalizedTime = 0;
                animationState.normalizedSpeed = 0;
                animationState.enabled = false;
                animationState.wrapMode = WrapMode.Clamp;
                animationState.layer = 1;
            }
        }

        public void PlayAnimation(string animationName, float animationTime, float animationSpeed)
        {
            //Plays the animation
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];
                animationState.normalizedTime = animationTime;
                animationState.normalizedSpeed = animationSpeed;
                animationState.enabled = true;
                animation.Play(animationName);
            }
        }

        public void QueueAnimation(string animationName, float animationTime, float animationSpeed)
        {
            //Queues an animation to play
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];
                animationState.normalizedTime = animationTime;
                animationState.normalizedSpeed = animationSpeed;
                animationState.enabled = true;
                animation.PlayQueued(animationName, QueueMode.CompleteOthers);
            }
        }

        //------------------------- Part GUI -------------------------
        [KSPEvent(guiActive = true, active = true, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Deploy Chute")]
        public void GUIDeploy()
        {
            //Forces the parachute to deploy
            this.part.force_activate();
        }

        [KSPEvent(guiActive = true, active = true, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Cut chute")]
        public void GUICut()
        {
            //Cuts chute
            Cut();
        }
        [KSPEvent(guiActive = false, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Repack chute", unfocusedRange = 5)]
        public void GUIRepack()
        {
            //Repacks chute from EVA if in space or on the ground
            if (CheckGroundStop() || this.vessel.atmDensity == 0)
            {
                if (deploymentState == "CUT")
                {
                    Repack();
                }
            }
        }

        //------------------------- Action groups -------------------------
        [KSPAction("Deploy chute")]
        public void ActionDeploy(KSPActionParam param)
        {
            //Forces the parachute to deploy
            this.part.force_activate();
        }

        [KSPAction("Cut chute")]
        public void ActionCut(KSPActionParam param)
        {
            //Cuts the chute
            Cut();
        }

        //------------------------- Methods -------------------------
        public bool CheckGroundStop()
        {
            //Checks if the vessel is on the ground and has stopped moving
            if (this.vessel.LandedOrSplashed && this.vessel.horizontalSrfSpeed < cutSpeed)
            {
                return true;
            }

            else
            {
                return false;
            }
        }

        public float GetTrueAlt()
        {
             //Gets the altitude from the ground or water
            CoM = vessel.findWorldCenterOfMass();
            up = (CoM - vessel.mainBody.position).normalized;
            ASL = (float)vessel.mainBody.GetAltitude(CoM);
            if (Physics.Raycast( CoM, -up, out source, ASL + 10000, 1 << 15))
            {
                altitude = Mathf.Min(source.distance, ASL);
            }
            else
            {
                altitude = ASL;
            }
            return altitude;
        }

        public bool CanDeployChute()
        {
            //Checks if the parachute can be deployed
            if (this.vessel.atmDensity > 0 && !CheckGroundStop() && GetTrueAlt() <= minDeploymentAlt)
            {
                return true;
            }

            else
            {
                return false;
            }
        }

        public bool isDeployed()
        {
            //Check if the chute is completely deployed
            if (deploymentState == "DEPLOYED" || deploymentState == "LOWDEPLOYED")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void ParachuteNoise()
        {
            //Gives a random noise to the parachute orientation
            var rotationAngle = new Vector3(5 * (Mathf.PerlinNoise(Time.time, 0) - 0.5f), 5 * (Mathf.PerlinNoise(Time.time, 10) - 0.5f), 5 * (Mathf.PerlinNoise(Time.time, 20) - 0.5f));
            parachute.Rotate(rotationAngle);
        }

        public Quaternion GetDragDirection()
        {
            //Returns the drag direction
            dragVector = -(Vector3)this.vessel.srf_velocity;
            CoM = part.rigidbody.position;
            up = (CoM - vessel.mainBody.position).normalized;
            parachuteRotation.SetLookRotation(dragVector, up);
            return parachuteRotation;
        }

        //------------------------- Deployment code -------------------------
        public void LowDeploy()
        {
            //Parachute low deployment code
            deploymentState = "LOWDEPLOYED";
            this.part.stackIcon.SetIconColor(XKCDColors.RadioactiveGreen);
            parachute.gameObject.SetActive(true);
            cap.gameObject.SetActive(false);
            capOff = true;
            PlayAnimation(preDeploymentAnimation, 0, 1 / preDeploymentSpeed);
            QueueAnimation(deploymentAnimation, 0, 1 / deploymentSpeed);
        }

        public void PreDeploy()
        {
            //Parachute predeployment code
            deploymentState = "PREDEPLOYED";
            this.part.stackIcon.SetIconColor(XKCDColors.BrightYellow);
            parachute.gameObject.SetActive(true);
            cap.gameObject.SetActive(false);
            capOff = true;
            PlayAnimation(preDeploymentAnimation, 0, 1 / preDeploymentSpeed);
        }

        public void Deploy()
        {
            //Parachute deployment code
            deploymentState = "DEPLOYED";
            this.part.stackIcon.SetIconColor(XKCDColors.RadioactiveGreen);
            PlayAnimation(deploymentAnimation, 0, 1 / deploymentSpeed);

        }

        public void StagingReset()
        {
            //Deactivates the part
            this.part.deactivate();
            this.part.inverseStage = Staging.CurrentStage;
            this.part.maximum_drag = stowedDrag;
        }

        public void Cut()
        {
            //Cuts the chute
            deploymentState = "CUT";
            this.part.stackIcon.SetIconColor(XKCDColors.Red);
            this.part.maximum_drag = stowedDrag;
            parachute.gameObject.SetActive(false);
            StagingReset();
        }

        public void Repack()
        {
            //Repacks a cut chute
            if (chuteCount != float.NaN && chuteCount > 0)
            {
                deploymentState = "STOWED";
                chuteCount--;
                this.part.stackIcon.SetIconColor(XKCDColors.White);
                cap.gameObject.SetActive(true);
                capOff = false;
            }
            else if (chuteCount == float.NaN)
            {
                deploymentState = "STOWED";
                this.part.stackIcon.SetIconColor(XKCDColors.White);
                cap.gameObject.SetActive(true);
                capOff = false;
            }
        }

        //------------------------- Drag code -------------------------
        public float DragDeployment(float time, float debutDrag, float endDrag)
        {
            //Calculates drag depending on how long since it's been deployed
            currentTime = Time.time;

            if (!timeSet)
            {
                debutTime = currentTime;
                timeSet = true;
            }
            deltaTime = currentTime - debutTime;

            if (deltaTime <= time)
            {
                deploymentTime = (Mathf.Exp(deltaTime) / Mathf.Exp(time)) * (deltaTime / time);
                drag = Mathf.Lerp(debutDrag, endDrag, deploymentTime);
                return drag;
            }

            else
            {
                return endDrag;
            }


        }

        //------------------------- Activation code -------------------------

        public override void OnStart(PartModule.StartState state)
        {
            //Initiates the part
            this.part.stagingIcon = "PARACHUTES";
            deploymentState = "STOWED";
            this.part.maximum_drag = stowedDrag;

            //Initiates animation
            this.cap = this.part.FindModelTransform(capName);
            this.parachute = this.part.FindModelTransform(parachuteName);
            parachute.gameObject.SetActive(false);
            InitiateAnimation(preDeploymentAnimation);
            InitiateAnimation(deploymentAnimation);

            //First initiation of the part
            if (!initiated)
            {
                initiated = true;
                capOff = false;
                cap.gameObject.SetActive(true);
                if (spareChutes >= 0)
                {
                    chuteCount = spareChutes;
                }
                else
                {
                    chuteCount = float.NaN;
                }
            }

            //If the part has been staged in the past
            if (capOff)
            {
                deploymentState = "CUT";
                cap.gameObject.SetActive(false);
            }
        }

        public override string GetInfo()
        {
            //Info in the editor part window
            infoList += String.Format("Stowed drag: {0}\n", stowedDrag);
            infoList += String.Format("Predeployed drag: {0}\n", preDeployedDrag);
            infoList += String.Format("Deployed drag: {0}\n", deployedDrag);
            infoList += String.Format("Minimum deployment altitude: {0}m\n", minDeploymentAlt);
            infoList += String.Format("Deployment altitude: {0}m\n", deploymentAlt);
            infoList += String.Format("Predeployment speed: {0}s\n", preDeploymentSpeed);
            infoList += String.Format("Deployment speed: {0}s\n", deploymentSpeed);
            infoList += String.Format("Autocut speed: {0}m/s\n", cutSpeed);

            if (spareChutes != float.NaN)
            {
                infoList += String.Format("Spare chutes: {0}", spareChutes);
            }
            return infoList;
        }

        public override void OnFixedUpdate()
        {
            //Deployment clauses and actions
            if (CanDeployChute())
            {
                if (deploymentState == "STOWED")
                {
                    if (GetTrueAlt() > deploymentAlt)
                    {
                        PreDeploy();
                    }

                    else
                    {
                        LowDeploy();
                    }
                }
                
                else if (deploymentState == "PREDEPLOYED")
                {
                    parachute.rotation = GetDragDirection();
                    ParachuteNoise();
                    if (this.part.maximum_drag != preDeployedDrag)
                    {
                       this.part.maximum_drag = DragDeployment(preDeploymentSpeed, stowedDrag, preDeployedDrag);
                    }

                    if (GetTrueAlt() <= deploymentAlt)
                    {
                        Deploy();
                        timeSet = false;
                    }
                }
                else if (deploymentState == "DEPLOYED")
                {
                    parachute.rotation = GetDragDirection();
                    ParachuteNoise();
                    if (this.part.maximum_drag != deployedDrag)
                    {
                       this.part.maximum_drag = DragDeployment(deploymentSpeed, preDeployedDrag, deployedDrag);
                    }

                }

                else if (deploymentState == "LOWDEPLOYED")
                {
                    parachute.rotation = GetDragDirection();
                    ParachuteNoise();
                    if (this.part.maximum_drag != deployedDrag)
                    {
                        this.part.maximum_drag = DragDeployment(preDeploymentSpeed + deploymentSpeed, stowedDrag, deployedDrag);
                    }
                }
            }

            else if (CheckGroundStop() && isDeployed())
            {
                Cut();
            }

            else
            {
                StagingReset();
            }
        }
    }
}