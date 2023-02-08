using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Serialization;

namespace Astroite.Shadow
{
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    public class EVSM : MonoBehaviour
    {
        // Depth
        public RenderTexture m_DepthTexture = null;
        private Camera m_DepthCamera = null;
        public Shader m_DepthShader = null;

        //View&Projection's matrix in light space.
        private Matrix4x4 m_LightVPMatrix;
        private Light m_DirectionalLight = null;

        // Shadow Setting
        public float m_MaxSceneHeight = 10;       
        public float m_MinSceneHeight = 0;
        
        public float PositiveExponent = 5.0f;
        public float NegativeExponent = 2.0f;
        
        // Blur Setting
        private Material m_BlurMaterial = null;
        private Shader m_GaussianBlurShader = null;
        [Range(0, 6)]
        public int DownSampleNum = 2;
        [Range(0.0f, 20.0f)]
        public float BlurSpreadSize = 3.0f;
        [Range(0, 8)]
        public int BlurIterations = 3;

        [HideInInspector]
        public int m_CullingMask = -1;
        [HideInInspector]
        public int m_CullingMaskSelection = 0;

        void Start()
        {
            m_DirectionalLight = GetComponent<Light>();
            m_DepthTexture = InitRenderTexture();
            m_DepthCamera = InitDepthCamera(gameObject, m_DepthTexture);

            m_DepthShader = Shader.Find("Astroite/EVSM/EVSMGenerator");
            m_GaussianBlurShader = Shader.Find("Astroite/Common/GaussianBlur");
            m_BlurMaterial = new Material(m_GaussianBlurShader);
        }

        void Update()
        {
            Graphics.SetRenderTarget(m_DepthTexture);
            GL.Clear(true, true, Color.white);

            if (null == m_DepthCamera)
            {
                Debug.Log("No Render Target");
            }
            if (null == m_DepthShader)
            {
                Debug.Log("No Depth Shader");
                return;
            }
            
            UpdateDepthCamera(Camera.main, m_DepthCamera, m_DirectionalLight);
            SetShaderGlobal();

            m_DepthCamera.RenderWithShader(m_DepthShader, "RenderType");
            BlurDepthRT(m_DepthTexture);
        }

        RenderTexture InitRenderTexture()
        {
            return AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/ArtSources/RenderTexture/ShadowMap.renderTexture");
        }

        Camera InitDepthCamera(GameObject lightObj, RenderTexture rt)
        {
            Camera depthCamera = lightObj.GetComponentInChildren<Camera>();
            if (!depthCamera)
            {
                GameObject depthCameraObj = new GameObject("DepthCamera");
                depthCamera = depthCameraObj.AddComponent<Camera>();
                depthCameraObj.transform.SetParent(lightObj.transform, false);
            }

            depthCamera.orthographic = true;
            depthCamera.backgroundColor = Color.black;
            depthCamera.clearFlags = CameraClearFlags.Color;
            depthCamera.enabled = false;
            depthCamera.targetTexture = rt;
            depthCamera.nearClipPlane = 1.0f;
            depthCamera.farClipPlane = 1000.0f;
            return depthCamera;
        }
        
        void UpdateDepthCamera(Camera mainCamera, Camera depthCamera, Light globalLight)
        {
            depthCamera.transform.rotation = globalLight.transform.rotation;
        }
        
        void SetShaderGlobal()
        {
            m_LightVPMatrix = GL.GetGPUProjectionMatrix(m_DepthCamera.projectionMatrix, false) * m_DepthCamera.worldToCameraMatrix;
            
            Shader.SetGlobalMatrix("_LightViewClipMatrix", m_LightVPMatrix);
            Shader.SetGlobalTexture("_ShadowDepthTex", m_DepthTexture);
            Shader.SetGlobalFloat("_PositiveExponent", PositiveExponent);
            Shader.SetGlobalFloat("_NegativeExponent", NegativeExponent);
        }
        
        void BlurDepthRT(RenderTexture renderTexture)
        {
            if (m_GaussianBlurShader != null)
            {
                float widthMod = 1.0f / (1.0f * (1 << DownSampleNum));
                m_BlurMaterial.SetFloat("_DownSampleValue", BlurSpreadSize * widthMod);
                int renderWidth = renderTexture.width >> DownSampleNum;
                int renderHeight = renderTexture.height >> DownSampleNum;

                RenderTexture renderBuffer = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, renderTexture.format);
                renderBuffer.filterMode = FilterMode.Bilinear;
                Graphics.Blit(renderTexture, renderBuffer, m_BlurMaterial, 0);

                for (int i = 0; i < BlurIterations; i++)
                {
                    float iterationOffs = (i * 1.0f);
                    m_BlurMaterial.SetFloat("_DownSampleValue", BlurSpreadSize * widthMod + iterationOffs);

                    RenderTexture tempBuffer = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, renderTexture.format);
                    Graphics.Blit(renderBuffer, tempBuffer, m_BlurMaterial, 1);
                    RenderTexture.ReleaseTemporary(renderBuffer);
                    renderBuffer = tempBuffer;

                    tempBuffer = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, renderTexture.format);
                    Graphics.Blit(renderBuffer, tempBuffer, m_BlurMaterial, 2);
                    RenderTexture.ReleaseTemporary(renderBuffer);
                    renderBuffer = tempBuffer;
                }

                Graphics.Blit(renderBuffer, renderTexture);
                RenderTexture.ReleaseTemporary(renderBuffer);
            }
        }
    }
}