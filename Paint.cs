using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;

using System.Linq;

namespace MyRevit
{
    // a Paint class with name and color
    class Paint
    {
        public string Name { get; set; }
        public Color Color { get; set; }

        public Paint(string name, Color color)
        {
            Name = name;
            Color = color;
        }

        public static void setup_paints(Document doc){
            // create an array of paints and their respective colors
            Paint[] paints = new Paint[] {
                new Paint("SW7050", new Color(207, 202, 189)),
                new Paint("SW6840", new Color(181, 77, 127)),
            };

            // Create a new paint material
            Transaction trans;
            using (trans = new Transaction(doc))
            {
                trans.Start("Paints");
                // create a new material for each paint
                AppearanceAssetElement assetElem = new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement)).First(x => x.Name == "Generic") as AppearanceAssetElement;
                foreach (Paint paint in paints)
                {
                    // Paint appearances use a generic asset
                    AppearanceAssetElement assetElem2 = assetElem.Duplicate("Generic " + paint.Name) as AppearanceAssetElement;

                    ElementId materialId = Material.Create(doc, paint.Name);
                    Material material = doc.GetElement(materialId) as Material;
                    material.MaterialClass = "Paint";
                    material.Color = paint.Color;
                    using(AppearanceAssetEditScope editScope = new AppearanceAssetEditScope( doc ))
                    {
                        Asset editableAsset = editScope.Start(assetElem2.Id);
                        AssetPropertyDoubleArray4d genericDiffuseProperty = editableAsset["generic_diffuse"] as AssetPropertyDoubleArray4d;
                        genericDiffuseProperty.SetValueAsColor(material.Color);
                        editScope.Commit(true);
                    }

                    material.AppearanceAssetId = assetElem2.Id;
                    material.UseRenderAppearanceForShading = true;
                }
                trans.Commit();
            }
        }
    }
}
