using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AnttiStarterKit.Visuals
{
    public class Blit : ScriptableRendererFeature
    {
        public class BlitPass : ScriptableRenderPass
        {
            public enum RenderTarget
            {
                Color,
                RenderTexture,
            }

            public Material blitMaterial = null;
            public int blitShaderPassIndex = 0;
            public FilterMode filterMode { get; set; }

            private RTHandle source { get; set; }
            private RTHandle destination { get; set; }
            private RTHandle m_TemporaryColorTexture;
            string m_ProfilerTag;

            public BlitPass(RenderPassEvent renderPassEvent, Material blitMaterial, int blitShaderPassIndex, string tag)
            {
                this.renderPassEvent = renderPassEvent;
                this.blitMaterial = blitMaterial;
                this.blitShaderPassIndex = blitShaderPassIndex;
                m_ProfilerTag = tag;
            }

            public void Setup(RTHandle source, RTHandle destination)
            {
                this.source = source;
                this.destination = destination;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
                opaqueDesc.depthBufferBits = 0;
                
                // Re-allocate temporary RT handle if descriptor changes
                RenderingUtils.ReAllocateIfNeeded(ref m_TemporaryColorTexture, opaqueDesc, filterMode, TextureWrapMode.Clamp, name: "_TemporaryColorTexture");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

                // Check if we are writing back to the camera target
                if (destination == null || destination.name == "_CameraColorTexture")
                {
                    Blit(cmd, source, m_TemporaryColorTexture, blitMaterial, blitShaderPassIndex);
                    Blit(cmd, m_TemporaryColorTexture, source);
                }
                else
                {
                    Blit(cmd, source, destination, blitMaterial, blitShaderPassIndex);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                // RTHandles allocated via ReAllocateIfNeeded should be released in Dispose(), not here
            }

            public void Dispose()
            {
                m_TemporaryColorTexture?.Release();
            }
        }

        [System.Serializable]
        public class BlitSettings
        {
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;
            public Material blitMaterial = null;
            public int blitMaterialPassIndex = -1;
            public Target destination = Target.Color;
            public string textureId = "_BlitPassTexture";
        }

        public enum Target
        {
            Color,
            Texture
        }

        public BlitSettings settings = new BlitSettings();
        RTHandle m_RenderTextureHandle;
        BlitPass blitPass;

        public override void Create()
        {
            var passIndex = settings.blitMaterial != null ? settings.blitMaterial.passCount - 1 : 1;
            settings.blitMaterialPassIndex = Mathf.Clamp(settings.blitMaterialPassIndex, -1, passIndex);
            
            blitPass = new BlitPass(settings.Event, settings.blitMaterial, settings.blitMaterialPassIndex, name);

            if (settings.destination == Target.Texture)
            {
                m_RenderTextureHandle = RTHandles.Alloc(settings.textureId, name: settings.textureId);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Use cameraColorTargetHandle for modern URP
            var src = renderer.cameraColorTargetHandle;
            var dest = (settings.destination == Target.Color) ? null : m_RenderTextureHandle;

            if (settings.blitMaterial == null)
            {
                Debug.LogWarningFormat("Missing Blit Material. {0} blit pass will not execute. Check for missing reference in the assigned renderer.", GetType().Name);
                return;
            }

            blitPass.Setup(src, dest);
            renderer.EnqueuePass(blitPass);
        }

        protected override void Dispose(bool disposing)
        {
            blitPass?.Dispose();
            m_RenderTextureHandle?.Release();
        }
    }
}