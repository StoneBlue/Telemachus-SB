﻿using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Telemachus.CameraSnapshots
{
    public class CameraCapture : MonoBehaviour
    {
        public RenderTexture overviewTexture;
        public bool didRender;
        public byte[] imageBytes = null;
        public bool mutex = false;

        public virtual string cameraManagerName()
        {
            return "NA";
        }

        public virtual string cameraType()
        {
            return "NA";
        }

        protected Dictionary<string, Camera> cameraDuplicates = new Dictionary<string, Camera>();
        protected List<string> activeCameras = new List<string>();
        protected static readonly string[] skippedCameras = { "UIMainCamera", "UIVectorCamera", "velocity camera" };
        private readonly string[] knownCameraNames =
        {
            "GalaxyCamera",
            "Camera ScaledSpace",
            "Camera VE Underlay", // Environmental Visual Enhancements plugin camera
            "Camera VE Overlay",  // Environmental Visual Enhancements plugin camera
            "Camera 01",
            "Camera 00",
            "InternalCamera",
            "FXCamera"
        };

        public Dictionary<string, Camera> gameCameraMapping = new Dictionary<string, Camera>();


        protected string cameraContainerNamePrefix {
            get
            {
                return "TelemachusCameraContainer:" + cameraManagerName();
            }
        }
        
        protected const float fovAngle = 60f;
        protected const float aspect = 1.0f;
        public  int cameraResolution = 300;

        protected virtual void LateUpdate()
        {
            //PluginLogger.debug("LATE UPDATE FOR FLIGHT CAMERA");
            if (CameraManager.Instance != null && HighLogic.LoadedSceneIsFlight)
            {
                duplicateAnyNewCameras();
                repositionCamera();
                StartCoroutine(newRender());
            }
        }

        public IEnumerator newRender()
        {
            yield return true;
            //PluginLogger.debug(cameraManagerName() +  ": WAITING FOR END OF FRAME");
            //yield return new WaitForEndOfFrame();
            //PluginLogger.debug(cameraManagerName() + ": OUT OF FRAME");
            Texture2D texture = getTexture2DFromRenderTexture();
            this.imageBytes = texture.EncodeToJPG();
            this.didRender = true;

            Destroy(texture);
        }

        public Texture2D getTexture2DFromRenderTexture()
        {
            Texture2D texture2D = new Texture2D(overviewTexture.width, overviewTexture.height);
            RenderTexture.active = overviewTexture;
            texture2D.ReadPixels(new Rect(0, 0, overviewTexture.width, overviewTexture.height), 0, 0);
            texture2D.Apply();
            return texture2D;
        }

        public void duplicateAnyNewCameras()
        {
            if (overviewTexture == null)
            {
                overviewTexture = new RenderTexture(cameraResolution, cameraResolution, 24);
            }

            List<string> currentlyActiveCameras = new List<string>();

            foreach (Camera camera in Camera.allCameras)
            {
                
                // Don't duplicate any cameras we're going to skip
                if (skippedCameras.IndexOf(camera.name) != -1)
                {
                    continue;
                }

                // Don't duplicate cameras we don't know about
                if (knownCameraNames.IndexOf(camera.name) == -1)
                {
                    continue;
                }


                if (!cameraDuplicates.ContainsKey(camera.name))
                {
                    var cameraDuplicateGameObject = new GameObject(cameraContainerNamePrefix + camera.name);
                    Camera cameraDuplicate = cameraDuplicateGameObject.AddComponent<Camera>();
                    cameraDuplicates[camera.name] = cameraDuplicate;
                    cameraDuplicate.CopyFrom(camera);
                    cameraDuplicate.fieldOfView = fovAngle;
                    cameraDuplicate.aspect = aspect;

                    cameraDuplicate.targetTexture = this.overviewTexture;
                    if (camera.name == "Camera 00" || camera.name == "FXCamera")
                    {
                        cameraDuplicate.nearClipPlane = cameraDuplicate.farClipPlane / 8192.0f;
                    }

                    //Now that the camera has been duplicated, add it to the list of active cameras
                    activeCameras.Add(camera.name);
                    if (!gameCameraMapping.ContainsKey(camera.name))
                    {
                        gameCameraMapping[camera.name] = camera;
                    }
                }

                //Mark that the camera is currently active
                currentlyActiveCameras.Add(camera.name);
            }

            if (currentlyActiveCameras.Count > 0 && activeCameras.Count > 0)
            {
                IEnumerable<string> disabledCameras = activeCameras.Except(currentlyActiveCameras);
                foreach (string disabledCamera in disabledCameras)
                {
                    if (cameraDuplicates.ContainsKey(disabledCamera))
                    {
                        Destroy(cameraDuplicates[disabledCamera]);
                        cameraDuplicates.Remove(disabledCamera);
                        activeCameras.Remove(disabledCamera);
                    }
                }
            }
        }


        public virtual void repositionCamera()
        {
           foreach (KeyValuePair<string, Camera> KVP in cameraDuplicates)
            {
                Camera cameraDuplicate = KVP.Value;
                Camera gameCamera = gameCameraMapping[KVP.Key];

                cameraDuplicate.transform.position = gameCamera.transform.position;
                cameraDuplicate.transform.rotation = gameCamera.transform.rotation;

                additionalCameraUpdates(cameraDuplicate, gameCamera);
            }
        }

        public void verboseCameraDebug(Camera camera)
        {
            string[] debugProperties = {
                        "CAMERA INFO: " + camera.name,
                        "TARGET DISPLAY: " + camera.targetDisplay,
                        "TARGET TEXTURE: " + camera.targetTexture,
                        "RENDERING PATH: " + camera.renderingPath,
                        "ACTUAL RENDER PATH: " + camera.actualRenderingPath,
                        "CAMERA TYPE: " + camera.cameraType,
                        "GAME OBJECT: " + camera.gameObject
                    };
            PluginLogger.debug(String.Join("\n", debugProperties));
        }

        /*public void UpdateCameras()
        {
            if (CameraManager.Instance != null)
            {
                //PluginLogger.debug("CURRENT CAMERA MODE: " + CameraManager.Instance.currentCameraMode);
            }

            activeCameras = new List<string>();
            PluginLogger.debug("UPDATING CAMERAS");
            foreach (Camera camera in Camera.allCameras)
            {
                // debugCameraDetails(camera);
                // Don't duplicate any cameras we're going to skip
                if (skippedCameras.IndexOf(camera.name) != -1)
                {
                    continue;
                }

                Camera cameraDuplicate;

                if (!cameraDuplicates.ContainsKey(camera.name))
                {
                    var cameraDuplicateGameObject = new GameObject(cameraContainerNamePrefix + camera.name);
                    cameraDuplicate = cameraDuplicateGameObject.AddComponent<Camera>();
                    cameraDuplicates[camera.name] = cameraDuplicate;
                }
                else
                {
                    cameraDuplicate = cameraDuplicates[camera.name];
                }

                cameraDuplicate.CopyFrom(camera);
                cameraDuplicate.enabled = false;
                cameraDuplicate.fieldOfView = fovAngle;
                cameraDuplicate.aspect = aspect;

                if (camera.name == "Camera 00" || camera.name == "FXCamera")
                {
                    //PluginLogger.debug("ADJUSTING NEAR CLIPPING PLANE FOR: " + camera.name + " : " + cameraDuplicate.farClipPlane / 8192.0f);
                    cameraDuplicate.nearClipPlane = cameraDuplicate.farClipPlane / 8192.0f;
                }

                additionalCameraUpdates(cameraDuplicate);

                //Now that the camera has been duplicated, add it to the list of active cameras
                activeCameras.Add(camera.name);
            }
        }*/

        public virtual void additionalCameraUpdates(Camera dupliateCam, Camera gameCamera){ }

        public virtual void debugCameraDetails(Camera cam)
        {
            PluginLogger.debug("CAMERA: " + cam.name + " POS: " + cam.transform.position + "; ROT: " + cam.transform.rotation  + " ; NEAR:" + cam.nearClipPlane + "; FAR: " + cam.farClipPlane);
        }

        public virtual void BeforeRenderNewScreenshot(){ }
        
        /*public IEnumerator NewScreenshot()
        {
            if (mutex)
            {
                yield return true;
            }
            //PluginLogger.debug("BYPASSED MUTEX");
            mutex = true;
            //PluginLogger.debug("WAITING FOR END OF FRAME");
            yield return new WaitForEndOfFrame();

            BeforeRenderNewScreenshot();

            List<Camera> renderingCameras = new List<Camera>();
            foreach (string cameraName in activeCameras)
            {
                //PluginLogger.debug("[" + cameraManagerName() + "] GETTING CAMERA " + cameraName);
                renderingCameras.Add(cameraDuplicates[cameraName]);
            }

            this.imageBytes = SnapshotRenderer.renderSnaphot(renderingCameras, cameraResolution, cameraResolution);
            this.didRender = true;
            mutex = false;
        }*/

    }
}