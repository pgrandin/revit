using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

using Autodesk.Revit.UI;
using Newtonsoft.Json;

namespace MyRevit
{
    class Roof : MyRevit.MyLevel
    {

        protected Document doc;
        protected IList<Level> levels = null;
        protected int level_id;
        protected Level level;

        public Roof(Document doc)
        {
            this.doc = doc;
            this.levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            this.level_id = 4;
            this.level = levels[level_id];
        }




        public Result setup_roof()
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(RoofType));
            RoofType roofType = collector.FirstElement() as RoofType;

            Transaction trans = new Transaction(doc);


            string jsonFilePath = @"C:\Users\John\source\repos\revit\data.json";

            List<mylevels> items;
            List<XYZ[]> coords = new List<XYZ[]>();

            using (StreamReader r = new StreamReader(jsonFilePath))
            {
                string json = r.ReadToEnd();
                items = JsonConvert.DeserializeObject<List<mylevels>>(json);
            }
            List<int> slopes = new List<int>();
            int i;

            foreach (mylevels l in items)
            {
                if (l.level == level.Name)
                {
                    dataset = l.sets;
                }
            }

            foreach (mysets s in dataset)
            {
                if (s.type == "roof")
                {
                    using (trans = new Transaction(doc))
                    {
                        trans.Start("roof");

                        CurveArray footprint = new CurveArray();
                        Level roof_level = level;
                        if (s.base_level != null)
                        {
                            roof_level = new FilteredElementCollector(doc)
                                .OfClass(typeof(Level))
                                .Cast<Level>().FirstOrDefault(q
                                => q.Name == s.base_level);
                        }

                        walls p = null;
                        foreach (walls w in s.walls)
                        {                                                        
                            XYZ a = w.coords[0] is null ?
                                new XYZ(p.coords[1][0] / 12.0, p.coords[1][1] / 12.0, roof_level.Elevation) :
                                new XYZ(w.coords[0][0] / 12.0, w.coords[0][1] / 12.0, roof_level.Elevation);
                            XYZ b = new XYZ(w.coords[1][0] / 12.0, w.coords[1][1] / 12.0, roof_level.Elevation);

                            Line line = Line.CreateBound(a, b);
                            footprint.Append(line);                           
                            p = w;
                        }
                        ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
                        FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footprint, roof_level, roofType, out footPrintToModelCurveMapping);

                        ModelCurveArrayIterator iterator = footPrintToModelCurveMapping.ForwardIterator();
                        iterator.Reset();
                        i = 0;
                        while (iterator.MoveNext())
                        {
                            ModelCurve modelCurve = iterator.Current as ModelCurve;
                            if (s.walls[i].define_slope)
                            {
                                footprintRoof.set_DefinesSlope(modelCurve, true);
                                // footprintRoof.set_SlopeAngle(modelCurve, 0.5);
                            }
                            if (s.walls[i].roof_offset != 0)
                            {
                                footprintRoof.set_Offset(modelCurve, s.walls[i].roof_offset / 12.0);
                            }
                            i++;
                        }

                        trans.Commit();
                    }
                }
            }
            
            return Result.Succeeded;
        }

    }
}
