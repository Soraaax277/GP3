using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class BokehGrabFeature : ScriptableRendererFeature
{
    BokehGrabPass _pass;

    static readonly int ID_BokehSourceTex = Shader.PropertyToID("_BokehSourceTex");

    public override void Create()
    {
        _pass = new BokehGrabPass();
        // After ALL post-processing — captures whatever era filter is active
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        renderer.EnqueuePass(_pass);
    }

    class BokehGrabPass : ScriptableRenderPass
    {
        static readonly int ID_BokehSourceTex = Shader.PropertyToID("_BokehSourceTex");

        class PassData
        {
            public TextureHandle src;
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer ctx)
        {
            var resourceData = ctx.Get<UniversalResourceData>();

            // Make a copy of the fully-composited frame (all era filters applied)
            var desc         = rg.GetTextureDesc(resourceData.activeColorTexture);
            desc.name        = "_BokehSourceTex";
            desc.clearBuffer = false;
            var grabbed      = rg.CreateTexture(desc);

            // Blit active → grabbed (plain copy, no material)
            using (var builder = rg.AddRasterRenderPass<PassData>("BokehGrab", out var data))
            {
                data.src = resourceData.activeColorTexture;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(grabbed, 0);
                builder.SetGlobalTextureAfterPass(grabbed, ID_BokehSourceTex);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), 0, false));
            }


        }
    }
}