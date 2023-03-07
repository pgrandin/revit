using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;

using System.Linq;
using System.Collections.Generic;

namespace MyRevit
{
    class Paint
    {
        public string Name { get; set; }
        public Color Color { get; set; }

        public Material Material { get; set; }

        public Paint(string name, Color color)
        {
            Name = name;
            Color = color;
            Material = null;
        }

        public static void setup_paints(Document doc, IList<Paint> paints) {
            // create an array of paints and their respective colors

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
                    using (AppearanceAssetEditScope editScope = new AppearanceAssetEditScope(doc))
                    {
                        Asset editableAsset = editScope.Start(assetElem2.Id);
                        // change the 'generic_diffuse' property to the paint color
                        AssetPropertyDoubleArray4d genericDiffuseProperty = editableAsset.FindByName("generic_diffuse") as AssetPropertyDoubleArray4d;
                        genericDiffuseProperty.SetValueAsColor(paint.Color);
                        editScope.Commit(true);
                    }

                    material.AppearanceAssetId = assetElem2.Id;
                    material.UseRenderAppearanceForShading = true;

                    // Set the material's manufacturer and manufacturer's material ID
                    var parameterManufacturer = material.get_Parameter(BuiltInParameter.ALL_MODEL_MANUFACTURER);
                    parameterManufacturer.Set("Sherwin Williams");

                    var parameterModel = material.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL);
                    parameterModel.Set(paint.Name);

                    paint.Material = material;
                }
                trans.Commit();
            }
        }
    }
}
