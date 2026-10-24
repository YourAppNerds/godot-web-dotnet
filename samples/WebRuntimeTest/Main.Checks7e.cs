using System;
using Godot;

// Step 7e checks: RenderingServer from C#. Before 7e any RenderingServer use threw from
// RenderingServer..cctor() on Web (NativeMethodBindNotFoundException for
// get_video_adapter_type, which web builds without RenderingDevice did not bind).
public partial class Main
{
    private void RunStep7eChecks()
    {
        Check("RenderingServer adapter info", () =>
        {
            string name = RenderingServer.GetVideoAdapterName();
            string vendor = RenderingServer.GetVideoAdapterVendor();
            var type = RenderingServer.GetVideoAdapterType();
            string api = RenderingServer.GetVideoAdapterApiVersion();
            string driver = RenderingServer.GetCurrentRenderingDriverName();
            string method = RenderingServer.GetCurrentRenderingMethod();
            return (!string.IsNullOrEmpty(driver),
                $"name='{name}' vendor='{vendor}' type={type} api='{api}' driver={driver} method={method}");
        });

        Check("RenderingServer.GetRenderingDevice (null without RD)", () =>
        {
            var rd = RenderingServer.GetRenderingDevice();
            var local = RenderingServer.CreateLocalRenderingDevice();
            return (rd == null && local == null, $"rd={(rd == null ? "null" : rd.GetClass())} local={(local == null ? "null" : local.GetClass())}");
        });

        Check("RenderingServer MultiMesh", () =>
        {
            Rid mm = RenderingServer.MultimeshCreate();
            try
            {
                RenderingServer.MultimeshAllocateData(mm, 16, RenderingServer.MultimeshTransformFormat.Transform3D, colorFormat: true);
                for (int i = 0; i < 16; i++)
                {
                    var xf = new Transform3D(Basis.Identity, new Vector3(i, 2 * i, -i));
                    RenderingServer.MultimeshInstanceSetTransform(mm, i, xf);
                    RenderingServer.MultimeshInstanceSetColor(mm, i, new Color(i / 16f, 0, 1));
                }
                int count = RenderingServer.MultimeshGetInstanceCount(mm);
                Vector3 origin = RenderingServer.MultimeshInstanceGetTransform(mm, 5).Origin;
                float[] buffer = RenderingServer.MultimeshGetBuffer(mm);
                // Transform3D (12 floats) + color (4 floats) per instance.
                bool ok = count == 16 && origin.IsEqualApprox(new Vector3(5, 10, -5)) && buffer.Length == 16 * 16;
                return (ok, $"count={count} origin[5]={origin} buffer={buffer.Length} floats");
            }
            finally
            {
                RenderingServer.FreeRid(mm);
            }
        });

        Check("RenderingServer mesh + canvas item + texture", () =>
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) };
            Rid mesh = RenderingServer.MeshCreate();
            Rid ci = RenderingServer.CanvasItemCreate();
            var image = Image.CreateEmpty(8, 4, false, Image.Format.Rgba8);
            image.Fill(Colors.Orange);
            Rid tex = RenderingServer.Texture2DCreate(image);
            try
            {
                RenderingServer.MeshAddSurfaceFromArrays(mesh, RenderingServer.PrimitiveType.Triangles, arrays);
                int surfaces = RenderingServer.MeshGetSurfaceCount(mesh);
                Aabb aabb = RenderingServer.MeshGetCustomAabb(mesh);

                RenderingServer.CanvasItemSetParent(ci, GetCanvasItem());
                RenderingServer.CanvasItemAddRect(ci, new Rect2(4, 4, 8, 8), Colors.Red);
                RenderingServer.CanvasItemAddTextureRect(ci, new Rect2(16, 4, 8, 4), tex);
                RenderingServer.CanvasItemSetVisible(ci, false);

                Image back = RenderingServer.Texture2DGet(tex);
                bool ok = surfaces == 1 && back != null && back.GetWidth() == 8 && back.GetHeight() == 4;
                return (ok, $"surfaces={surfaces} customAabb={aabb.Size} texture={back?.GetWidth()}x{back?.GetHeight()}");
            }
            finally
            {
                RenderingServer.FreeRid(ci);
                RenderingServer.FreeRid(mesh);
                RenderingServer.FreeRid(tex);
            }
        });
    }
}
