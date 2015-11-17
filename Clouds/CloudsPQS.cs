﻿using EVEManager;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Utils;
using PQSManager;

namespace Atmosphere
{
    public class CloudsPQS : PQSMod
    {
        private String body;
        private float altitude;
        CloudsVolume layerVolume = null;
        Clouds2D layer2D = null;
        CloudsMaterial cloudsMaterial = null;
        CelestialBody celestialBody = null;
        Transform scaledCelestialTransform = null;

        Callback onExitMapView;
        private bool volumeApplied = false;
        private float radius;

        Vector3 detailPeriod;
        Vector3 mainPeriod;
        Vector3 offset;


        public new bool enabled
        {
            get
            {
                return base.enabled;
            }
            set
            {
                base.enabled = value;
                if (layer2D != null)
                {
                    layer2D.enabled = value;
                }
                if (layerVolume != null)
                {
                    layerVolume.enabled = value;
                }
            }
        }

        public override void OnSphereActive()
        {

            CloudsManager.Log("CloudsPQS: ("+this.name+") OnSphereActive");
            if (layer2D != null)
            {
                layer2D.Scaled = false;
            }
            if (!volumeApplied)
            {
                if (layerVolume != null)
                {
                    layerVolume.Apply(cloudsMaterial, (float)celestialBody.Radius + altitude, celestialBody.transform);
                }
                volumeApplied = true;
            }
        }
        public override void OnSphereInactive()
        {
            CloudsManager.Log("CloudsPQS: (" + this.name + ") OnSphereInactive");
            if (layer2D != null)
            {
                layer2D.Scaled = true;
            }
            
            if (!MapView.MapIsEnabled)
            {
                if (layerVolume != null)
                {
                    layerVolume.Remove();
                }
                volumeApplied = false;
            }
        }

        protected void OnExitMapView()
        {
            StartCoroutine(CheckForDisable());
        }

        IEnumerator CheckForDisable()
        {
            yield return new WaitForFixedUpdate();
            if (!sphere.isActive)
            {
                if (layerVolume != null)
                {
                    layerVolume.Remove();
                }
                volumeApplied = false;
            }
            else
            {
                OnSphereActive();
            }
        }

        protected void Update()
        {
            bool visible = HighLogic.LoadedScene == GameScenes.TRACKSTATION || HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.SPACECENTER;

            float ut = (float)Planetarium.GetUniversalTime();
            Vector3 detailRotation = (ut * detailPeriod);
            detailRotation -= new Vector3((int)detailRotation.x, (int)detailRotation.y, (int)detailRotation.z);
            detailRotation *= 360;
            detailRotation += offset;
            Vector3 mainRotation = (ut * mainPeriod);
            mainRotation -= new Vector3((int)mainPeriod.x, (int)mainPeriod.y, (int)mainPeriod.z);
            mainRotation *= 360f;
            mainRotation += offset;

            Quaternion rotation = Quaternion.Euler(mainRotation);
            Matrix4x4 mainRotationMatrix = Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one);

            rotation = Quaternion.Euler(detailRotation);
            Matrix4x4 detailRotationMatrix = Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one);

            if (this.sphere != null && visible)
            {
                if (HighLogic.LoadedScene == GameScenes.SPACECENTER || (HighLogic.LoadedScene == GameScenes.FLIGHT && sphere.isActive && !MapView.MapIsEnabled))
                {
                    if (layer2D != null)
                    {
                        layer2D.UpdateRotation(Quaternion.FromToRotation(Vector3.up, this.sphere.relativeTargetPosition), 
                                               this.sphere.transform.worldToLocalMatrix,
                                               mainRotationMatrix,
                                               detailRotationMatrix);
                    }
                }
                else 
                {
                    Transform transform = ScaledCamera.Instance.camera.transform;
                    Vector3 pos = scaledCelestialTransform.InverseTransformPoint(transform.position);
                    if (layer2D != null)
                    {
                        layer2D.UpdateRotation(Quaternion.FromToRotation(Vector3.up, pos),
                                               scaledCelestialTransform.transform.worldToLocalMatrix,
                                               mainRotationMatrix,
                                               detailRotationMatrix);
                    }
                }
                if (layerVolume != null && sphere.isActive)
                {
                    if(FlightCamera.fetch != null)
                    {
                        layerVolume.UpdatePos(FlightCamera.fetch.mainCamera.transform.position,
                                               Quaternion.Euler(-mainRotation),
                                               mainRotationMatrix,
                                               detailRotationMatrix);
                    }
                    else
                    {
                        layerVolume.UpdatePos(this.sphere.target.position,
                                               Quaternion.Euler(-mainRotation),
                                               mainRotationMatrix,
                                               detailRotationMatrix);
                    }
                }
            }
        }

        internal void Apply(String body, CloudsMaterial cloudsMaterial, Clouds2D layer2D, CloudsVolume layerVolume, float altitude, Vector3 speed, Vector3 detailSpeed, Vector3 offset)
        {
            this.body = body;
            this.cloudsMaterial = cloudsMaterial;
            this.layer2D = layer2D;
            this.layerVolume = layerVolume;
            this.altitude = altitude;
            this.offset = offset;

            celestialBody = Tools.GetCelestialBody(body);
            scaledCelestialTransform = Tools.GetScaledTransform(body);
            PQS pqs = null;
            if (celestialBody != null && celestialBody.pqsController != null)
            {
                pqs = celestialBody.pqsController;
            }
            else
            {
                CloudsManager.Log("No PQS! Instanciating one.");
                pqs = PQSManagerClass.GetPQS(body);
            }
            CloudsManager.Log("PQS Applied");
            if (pqs != null)
            {
                this.sphere = pqs;
                this.transform.parent = pqs.transform;
                this.requirements = PQS.ModiferRequirements.Default;
                this.modEnabled = true;
                this.order += 10;

                this.transform.localPosition = Vector3.zero;
                this.transform.localRotation = Quaternion.identity;
                this.transform.localScale = Vector3.one;
                this.radius = (float)(altitude + celestialBody.Radius);
                
                
                float circumference = 2f * Mathf.PI * radius;
                mainPeriod = (speed) / circumference;
                detailPeriod = (detailSpeed) / circumference;
                
                if (layer2D != null)
                {
                    this.layer2D.Apply(celestialBody, scaledCelestialTransform, cloudsMaterial, radius);
                }
                if (!pqs.isActive || HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                {
                    this.OnSphereInactive();
                }
                else
                {
                    this.OnSphereActive();
                }
                this.OnSetup();
                pqs.EnableSphere();
            }
            else
            {
                CloudsManager.Log("PQS is null somehow!?");
            }
            onExitMapView = new Callback(OnExitMapView);
            MapView.OnExitMapView += onExitMapView;
            GameEvents.onGameSceneLoadRequested.Add(GameSceneLoaded);
        }

        private void GameSceneLoaded(GameScenes scene)
        {
            if (scene != GameScenes.SPACECENTER && scene != GameScenes.FLIGHT)
            {
                this.OnSphereInactive();
                sphere.isActive = false;
            }
            if (scene != GameScenes.SPACECENTER && scene != GameScenes.FLIGHT && scene != GameScenes.TRACKSTATION)
            {
                this.OnSphereInactive();
                sphere.isActive = false;
                this.enabled = false;
            }
            else
            {
                this.enabled = true;
            }
        }
        
        public void Remove()
        {
            if (layer2D != null)
            {
                layer2D.Remove();
            }
            if (layerVolume != null)
            {
                layerVolume.Remove();
            }
            layer2D = null;
            layerVolume = null;
            volumeApplied = false;
            this.sphere = null;
            this.enabled = false;
            this.transform.parent = null;
            MapView.OnExitMapView -= onExitMapView;
            GameEvents.onGameSceneLoadRequested.Remove(GameSceneLoaded);
        }
    }
}
