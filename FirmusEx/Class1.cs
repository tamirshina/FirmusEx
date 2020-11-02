using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.UI.Selection;

namespace FirmusEx
{

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class GetResults : IExternalCommand
    {
        public Autodesk.Revit.UI.Result Execute(ExternalCommandData revit,
           ref string message, ElementSet elements)
        {
			UIDocument uiDoc = revit.Application.ActiveUIDocument;
			Document doc = uiDoc.Document;

			List<Element> tableElments = getAllTablesDataNotLinked(revit);

			List<ResultClass> resultList = new List<ResultClass>();
			List<string> resultsStrings = new List<string>();


            foreach(Element el in tableElments)
            {
				List<Solid> geometrySolidsList = getSolidsListOfElement(revit, el);

				Solid largestSolid = LargestSolidElement(geometrySolidsList);

				FaceArray faces = largestSolid.Faces;
				PlanarFace topFace = TopFaceOriantasion(faces);

				Solid preTransformSolid = GeometryCreationUtilities.CreateExtrusionGeometry(topFace.GetEdgesAsCurveLoops(), 
					topFace.FaceNormal, 1);

				var tableFamilyInstance = el as FamilyInstance;
				Solid solid = SolidUtils.CreateTransformed(preTransformSolid, tableFamilyInstance.GetTransform());
				PaintSolid(doc, solid, 1);

				List<Element> temIntersectingList = getIntersectingSolidElements(solid, uiDoc, doc);
				List<string> itersectingElms = new List<string>();

				if (temIntersectingList.Any()) {
					foreach (Element intsecEl in temIntersectingList)
					{
						itersectingElms.Add(intsecEl.Name);
					}
				}
				ResultClass resClass = new ResultClass(Int32.Parse(el.Id.ToString()),
					el.Document.Title, itersectingElms.Any(), itersectingElms);

				resultsStrings.Add(resClass.instancePrint());
            }
			string finalMessage = "";
			foreach (string str in resultsStrings)
			{
				finalMessage += str + Environment.NewLine;
			}
			TaskDialog.Show("revit", finalMessage + Environment.NewLine + "Done in host model");

			List<Element> linkedTables = getLinkedDocFurniture(doc);
			List<string> linkedResultsStrings = new List<string>();

			var linkedTransformed = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance))
				.Select(lm =>
				{
					var linkedModel = ((RevitLinkInstance)lm);
					return linkedModel.GetTransform();
				})
				.FirstOrDefault();

			foreach (Element el in linkedTables)
			{
				List<Solid> geometrySolidsList = getSolidsListOfElement(revit, el);
				Solid largestSolid = LargestSolidElement(geometrySolidsList);
				FaceArray faces = largestSolid.Faces;
				PlanarFace topFace = TopFaceOriantasion(faces);
				Solid preTransformSolid = GeometryCreationUtilities.CreateExtrusionGeometry(
				topFace.GetEdgesAsCurveLoops(), topFace.FaceNormal, 1);

				var tableInstance = el as FamilyInstance;
				var testSome = el as Instance;

				Solid customTransformedSolid = TransformSolid(tableInstance.GetTransform(), linkedTransformed, preTransformSolid);

				PaintSolid(doc, customTransformedSolid, 1);

				List<Element> interSectsInLinked = getIntersectingSolidElements(customTransformedSolid, uiDoc, doc);
				List<string> linkedItersectingElms = new List<string>();

				if (interSectsInLinked.Any())
				{
					foreach (Element intsecEl in interSectsInLinked)
					{
						linkedItersectingElms.Add(intsecEl.Name);
					}
				}
				ResultClass resClass = new ResultClass(Int32.Parse(el.Id.ToString()),
					el.Document.Title, interSectsInLinked.Any(), linkedItersectingElms);

				linkedResultsStrings.Add(resClass.instancePrint());
			}
			string linkeFinalMessage = "";
			foreach (string str in linkedResultsStrings)
			{
				linkeFinalMessage += str + Environment.NewLine;
			}
			TaskDialog.Show("revit", linkeFinalMessage + Environment.NewLine + "Done in linked model");


			return Autodesk.Revit.UI.Result.Succeeded;
        }

        private List<Element> getAllTablesDataNotLinked(ExternalCommandData revit)
        {
			UIDocument uiDoc = revit.Application.ActiveUIDocument;
			Document doc = uiDoc.Document;

			ElementCategoryFilter furnitureFilter = new ElementCategoryFilter(BuiltInCategory.OST_Furniture);

			List<Element> tablesNotLinked = new List<Element>();

			string report = string.Empty;

			foreach (Element e in new FilteredElementCollector(doc)
					.OfClass(typeof(FamilyInstance))
					.WherePasses(furnitureFilter))
			{
				FamilyInstance fi = e as FamilyInstance;
				FamilySymbol fs = fi.Symbol;
				Family fam = fs.Family;

				if (fam.Name.Contains("Table"))
				{
					tablesNotLinked.Add(e);

					report += "\nName = " + fam.Name + " Element Id: " + e.Id.ToString();
				}
			}

			TaskDialog.Show("getAllTables", report);
			return tablesNotLinked;
		}
		private List<Solid> getSolidsListOfElement(ExternalCommandData revit, Element baseEl)
		{
			UIDocument uiDoc = revit.Application.ActiveUIDocument;
			Document doc = uiDoc.Document;

			GeometryElement geometryElement = baseEl.get_Geometry(new Options() { IncludeNonVisibleObjects = true, ComputeReferences = true, View = doc.ActiveView });

			List<Solid> geometrySolidsList = new List<Solid>();

			foreach (GeometryObject geometryObject in geometryElement)
			{

				GeometryInstance geoInst = (geometryObject as GeometryInstance);

				foreach (GeometryObject instObj in geoInst.SymbolGeometry)
				{
					Solid solid = instObj as Solid;

					geometrySolidsList.Add(solid);
				}
					}
			return geometrySolidsList;
				}
		private Solid LargestSolidElement(List<Solid> solidList)
		{

			Double max = 0;
			int index = -1;
			int maxIndex = -1;

			foreach (Solid solid in solidList)
			{
				index++;

				if (solid.SurfaceArea > max)
				{
					max = solid.SurfaceArea;
					maxIndex = index;
				}
			}

			return solidList[maxIndex];
		}
		private PlanarFace TopFaceOriantasion(FaceArray faces)
		{
			foreach (Face face in faces)
			{
				PlanarFace planarFace = face as PlanarFace;
				if (planarFace.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
				{
					return planarFace;
				}
			}
			return null;
		}
		public void PaintSolid(Document doc, Solid s, double value)
		{
			int schemaId = -1;
			var rnd = new Random();

			View view = doc.ActiveView;

			using (Transaction transaction = new Transaction(doc))
			{
				if (transaction.Start("Create model curves") == TransactionStatus.Started)
				{
					if (view.AnalysisDisplayStyleId == ElementId.InvalidElementId)
						CreateAVFDisplayStyle(doc, view);

					SpatialFieldManager sfm = SpatialFieldManager.GetSpatialFieldManager(view);
					if (null == sfm)
						sfm = SpatialFieldManager.CreateSpatialFieldManager(view, 1);

					if (-1 != schemaId)
					{
						IList<int> results = sfm.GetRegisteredResults();
						if (!results.Contains(schemaId))
							schemaId = -1;
					}
					if (-1 == schemaId)
					{

						AnalysisResultSchema resultSchema1 = new AnalysisResultSchema(rnd.Next().ToString(), "Description");
						schemaId = sfm.RegisterResult(resultSchema1);
					}

					FaceArray faces = s.Faces;
					Transform trf = Transform.Identity;
					foreach (Face face in faces)
					{
						int idx = sfm.AddSpatialFieldPrimitive(face, trf);
						IList<UV> uvPts = new List<UV>();
						List<double> doubleList = new List<double>();
						IList<ValueAtPoint> valList = new List<ValueAtPoint>();
						BoundingBoxUV bb = face.GetBoundingBox();
						uvPts.Add(bb.Min);
						doubleList.Add(value);
						valList.Add(new ValueAtPoint(doubleList));

						FieldDomainPointsByUV pnts = new FieldDomainPointsByUV(uvPts);

						FieldValues vals = new FieldValues(valList);
						sfm.UpdateSpatialFieldPrimitive(idx, pnts, vals, schemaId);
					}
					transaction.Commit();
				}
			}
		}
		private void CreateAVFDisplayStyle(Document doc, View view)
		{
			AnalysisDisplayColoredSurfaceSettings coloredSurfaceSettings = new AnalysisDisplayColoredSurfaceSettings();
			coloredSurfaceSettings.ShowGridLines = true;

			AnalysisDisplayColorSettings colorSettings = new AnalysisDisplayColorSettings();
			AnalysisDisplayLegendSettings legendSettings = new AnalysisDisplayLegendSettings();

			legendSettings.ShowLegend = false;
			var rnd = new Random();
			AnalysisDisplayStyle analysisDisplayStyle = AnalysisDisplayStyle.CreateAnalysisDisplayStyle(doc, "Paint Solid-" + rnd.Next(), coloredSurfaceSettings, colorSettings, legendSettings);
			view.AnalysisDisplayStyleId = analysisDisplayStyle.Id;
		}
		public List<Element> getIntersectingSolidElements(Solid solidsTointersect, UIDocument uidoc, Document doc)
		{

			FilteredElementCollector collector = new FilteredElementCollector(doc);

			List<Element> intersectingElements = collector

				.WherePasses(new ElementIntersectsSolidFilter(solidsTointersect))
				//.WhereElementIsNotElementType().OfCategory(BuiltInCategory.OST_LightingFixtures)
				.ToList();
			List<Element> noLightsIntersects = new List<Element>();

			foreach (Element el in intersectingElements)
			{
				if (!el.Name.Contains("Cooper"))
				{
					noLightsIntersects.Add(el);
				}
			}
			return noLightsIntersects;
		}
		public List<Element> getLinkedDocFurniture(Document doc)
		{
			List<Element> linkedFurniture = new List<Element>();
			IList<Element> linkedElemList = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks).OfClass(typeof(RevitLinkType)).ToElements();
			foreach (Element e in linkedElemList)
			{
				RevitLinkType linkType = e as RevitLinkType;
				foreach (Document linkedDoc in doc.Application.Documents)
				{
					if ((linkedDoc.Title + ".rvt").Equals(linkType.Name))
					{
						ElementCategoryFilter furnitureFilter = new ElementCategoryFilter(BuiltInCategory.OST_Furniture);
						foreach (Element linkedEl in new FilteredElementCollector(linkedDoc)
							.OfClass(typeof(FamilyInstance))
							.WherePasses(furnitureFilter))
						{
							linkedFurniture.Add(linkedEl);
						}
					}
				}

			}
			return linkedFurniture;
		}
		public Solid TransformSolid(Transform targetTransform, Transform sourceTransform, Solid solid)
		{
			var transform = targetTransform.Multiply(sourceTransform);
			var solidInTargetModel = SolidUtils.CreateTransformed(solid, transform);
			return solidInTargetModel;
		}

	}

}
