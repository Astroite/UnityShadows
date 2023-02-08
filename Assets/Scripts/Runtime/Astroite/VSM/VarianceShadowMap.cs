/******************************************************************************
 * -- Author        :   Ryan Zheng
 * -- Company       :   Metek 
 * -- Date          :   2018-01-29
 * -- Description   :   基本ShadowMap实现
 *  1, 实现阴影(√)
 *  2, RenderTexture设置边缘Color避免阴影被拉伸现象 (Discard border in shader.)(√)
 *  3, MainCamera远平面的及时调整, 避免阴影质量太差的问题(UpdateClipPlane & UpdateDepthCamera)(√)
 *  4, 移动过程中阴影抖动的问题(TODO:)(×)
 * 
******************************************************************************/

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;

namespace Astroite.Shadow
{
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    public class BaseShadowMap : MonoBehaviour
    {
        private int m_DepthTextureWidth = 1024;
        private int m_DepthTextureHeight = 1024;
        private RenderTexture m_DepthTexture = null;
        private Camera m_DepthCamera = null;

        //View&Projection's matrix in light space.
        private Matrix4x4 m_LightVPMatrix;
        private Light m_DirectionalLight = null;

        public float m_MaxSceneHeight = 10;       
        public float m_MinSceneHeight = 0;
        
        private Shader m_CaptureDepthShader = null;
        private Material m_BlurMaterial = null;
        public Shader m_BlurShader = null;
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
            m_CaptureDepthShader = Shader.Find("Astroite/VSM/GenerateDepth");

            m_BlurShader = Shader.Find("Astroite/Common/GaussianBlur");
            m_BlurMaterial = new Material(m_BlurShader);
        }

        void Update()
        {
            Graphics.SetRenderTarget(m_DepthTexture);
            GL.Clear(true, true, Color.white);

            if (null == m_DepthCamera) return;
            if (null == m_CaptureDepthShader) return;

            // UpdateClipPlane(Camera.main, m_MinSceneHeight - 1, m_MaxSceneHeight + 1);
            GetCameraViewCrossPoint(Camera.main);

            DrawViewFrustum(Camera.main);
            UpdateDepthCamera(Camera.main, m_DepthCamera, m_DirectionalLight);

            SetShaderGlobal();

            m_DepthCamera.RenderWithShader(m_CaptureDepthShader, "RenderType");
            BlurDepthRT(m_DepthTexture);
        }

        RenderTexture InitRenderTexture()
        {
            // return AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/ArtSources/RenderTexture/ShadowMap.renderTexture");
            RenderTexture rt = new RenderTexture(m_DepthTextureWidth, m_DepthTextureHeight, 0, RenderTextureFormat.RGFloat)
            {
                useMipMap = true
            };
            rt.Create();
            return rt;
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
            depthCamera.farClipPlane = 10.0f;
            depthCamera.cullingMask = m_CullingMask;

            return depthCamera;
        }

        /// <summary>
        /// 初始化RenderTexture和方向光投影矩阵信息传递到Shader中.
        /// </summary>
        void SetShaderGlobal()
        {
            Matrix4x4 world2View = m_DepthCamera.worldToCameraMatrix;
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(m_DepthCamera.projectionMatrix, false);
            m_LightVPMatrix = projection * world2View;

            Vector4 mainLightPosWS = new Vector4(
                m_DepthCamera.transform.position.x,
                m_DepthCamera.transform.position.y,
                m_DepthCamera.transform.position.z,
                1);

            Shader.SetGlobalVector("_MainLightPosWS", mainLightPosWS);
            Shader.SetGlobalTexture("_ShadowDepthTex", m_DepthTexture);
            Shader.SetGlobalMatrix("_LightViewClipMatrix", m_LightVPMatrix);
        }

        /// <summary>
        /// 计算摄像机视锥和上下界面的交点
        /// </summary>
        /// <param name="mainCamera"></param>
        /// <returns></returns>
        List<Vector4> GetCameraViewCrossPoint(Camera mainCamera)
        {
            Vector4 camWorldPos = mainCamera.transform.position;
            float angle = (mainCamera.fieldOfView / 180) * Mathf.PI;
            float nDis = mainCamera.nearClipPlane;

            // 近裁剪面
            List<Vector4> nearClipPoints = new List<Vector4>
            {
                new Vector4(nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, -nDis * Mathf.Tan(angle / 2), nDis, 1),
                new Vector4(nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, nDis * Mathf.Tan(angle / 2), nDis, 1),
                new Vector4(-nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, nDis * Mathf.Tan(angle / 2), nDis, 1),
                new Vector4(-nDis * Mathf.Tan(angle / 2) * mainCamera.aspect, -nDis * Mathf.Tan(angle / 2), nDis, 1),
                new Vector4(0, 0, nDis, 1)
            };

            // 转换到世界空间
            Matrix4x4 local2WorldMatrix = mainCamera.transform.localToWorldMatrix;
            for (int i = 0; i < nearClipPoints.Count; i++)
                nearClipPoints[i] = local2WorldMatrix * nearClipPoints[i];

            // 视锥的八个角
            List<Vector4> viewFrustrumPoints = new List<Vector4>();
            for (int i = 0; i < 4; i++)
                viewFrustrumPoints.Add(GetLinePlaneCrossPoint(camWorldPos, nearClipPoints[i], m_MaxSceneHeight));
            for (int i = 0; i < 4; i++)
                viewFrustrumPoints.Add(GetLinePlaneCrossPoint(camWorldPos, nearClipPoints[i], m_MinSceneHeight));

            return viewFrustrumPoints;
        }
        Vector4 GetLinePlaneCrossPoint(Vector4 begin, Vector4 end, float plane)
        {
            float t = (plane - end.y) / (end.y - begin.y);
            float x = end.x + t * (end.x - begin.x);
            float z = end.z + t * (end.z - begin.z);
            return new Vector4(x, plane, z, 1);
        }

        public void DrawViewFrustum(Camera mainCamera)
        {

            List<Vector4> crossPoints = GetCameraViewCrossPoint(mainCamera);
            for (int i = 0; i < 4; i++)
            {
                Vector3 start = new Vector3(crossPoints[i].x, crossPoints[i].y, crossPoints[i].z);
                int j = (i + 1) % 4;
                Vector3 end = new Vector3(crossPoints[j].x, crossPoints[j].y, crossPoints[j].z);
                Debug.DrawLine(start, end);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 start = new Vector3(crossPoints[i + 4].x, crossPoints[i + 4].y, crossPoints[i + 4].z);
                int j = (i + 1) % 4;
                Vector3 end = new Vector3(crossPoints[j + 4].x, crossPoints[j + 4].y, crossPoints[j + 4].z);
                Debug.DrawLine(start, end);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 start = new Vector3(crossPoints[i].x, crossPoints[i].y, crossPoints[i].z);
                int j = i + 4;
                Vector3 end = new Vector3(crossPoints[j].x, crossPoints[j].y, crossPoints[j].z);
                Debug.DrawLine(start, end);
            }
        }

        void UpdateDepthCamera(Camera mainCamera, Camera depthCamera, Light globalLight)
        {
            List<Vector4> mainClipPoints = GetCameraViewCrossPoint(mainCamera);

            Vector2 xSection = new Vector2(int.MinValue, int.MaxValue);
            Vector2 ySection = new Vector2(int.MinValue, int.MaxValue);
            Vector2 zSection = new Vector2(int.MinValue, int.MaxValue);
            Matrix4x4 world2LightMatrix = globalLight.transform.worldToLocalMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector4 point = world2LightMatrix * mainClipPoints[i];
                mainClipPoints[i] = point;

                if (point.x > xSection.x)
                    xSection.x = point.x;
                if (point.x < xSection.y)
                    xSection.y = point.x;

                if (point.y > ySection.x)
                    ySection.x = point.y;
                if (point.y < ySection.y)
                    ySection.y = point.y;

                if (point.z > zSection.x)
                    zSection.x = point.z;
                if (point.z < zSection.y)
                    zSection.y = point.z;
            }

            depthCamera.transform.localPosition = new Vector3((xSection.x + xSection.y) / 2, (ySection.x + ySection.y) / 2, zSection.y);
            depthCamera.nearClipPlane = 0;
            depthCamera.farClipPlane = zSection.x - zSection.y;
            depthCamera.orthographicSize = (ySection.x - ySection.y) / 2;
            depthCamera.aspect = (xSection.x - xSection.y) / (ySection.x - ySection.y);
        }

        void BlurDepthRT(RenderTexture renderTexture)
        {
            if (m_BlurShader != null)
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

    [CustomEditor(typeof(BaseShadowMap))]
    public class ShadowMapInspectorExtension : Editor
    {
        static List<string> options = new List<string>();

        public override void OnInspectorGUI()
        {
            BaseShadowMap shadowMap = (BaseShadowMap)target;

            DrawDefaultInspector();

            //if (shadowMap.m_CullingMask < 0)
            {
                options.Clear();
                for (int i = 0; i < 32; i++)
                {
                    string name = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(name) && !options.Contains(name))
                        options.Add(name);
                }
            }
            shadowMap.m_CullingMaskSelection = EditorGUILayout.MaskField("Culling Mask", shadowMap.m_CullingMaskSelection, options.ToArray());

            if (shadowMap.m_CullingMaskSelection >= 0)
            {
                shadowMap.m_CullingMask = 0;
                var myBitArray = new BitArray(BitConverter.GetBytes(shadowMap.m_CullingMaskSelection));

                for (int i = myBitArray.Count - 1; i >= 0; i--)
                {
                    if (myBitArray[i] == true)
                    {
                        if (shadowMap.m_CullingMask > 0)
                            shadowMap.m_CullingMask |= 1 << LayerMask.NameToLayer(options[i]);
                        else
                            shadowMap.m_CullingMask = 1 << LayerMask.NameToLayer(options[i]);
                    }

                }
            }
            else
                shadowMap.m_CullingMask = shadowMap.m_CullingMaskSelection;

            //Debug.Log("shadowMap.m_CullingMaskSelection :" + shadowMap.m_CullingMaskSelection);
        }
    }
}