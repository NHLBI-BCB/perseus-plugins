using System;
using System.Collections.Generic;
using System.Drawing;
using BaseLib.Param;
using BaseLibS.Num;
using BaseLibS.Param;
using PerseusApi.Document;
using PerseusApi.Generic;
using PerseusApi.Matrix;
using PerseusPluginLib.Properties;
using PerseusPluginLib.Utils;

namespace PerseusPluginLib.Filter{
	internal enum FilteringMode{
		Valid,
		GreaterThan,
		GreaterEqualThan,
		LessThan,
		LessEqualThan,
		Between,
		Outside
	}

	public class FilterValidValuesRows : IMatrixProcessing{
		public bool HasButton => true;
		public Bitmap DisplayImage => Resources.missingsButton_Image;
		public string Name => "Filter rows based on valid values";
		public string Heading => "Filter rows";
		public bool IsActive => true;
		public float DisplayRank => 3;
		public string[] HelpSupplTables => new string[0];
		public int NumSupplTables => 0;
		public string[] HelpDocuments => new string[0];
		public int NumDocuments => 0;

		public int GetMaxThreads(Parameters parameters){
			return 1;
		}

		public string Url
			=> "http://coxdocs.org/doku.php?id=perseus:user:activities:MatrixProcessing:Filterrows:FilterValidValuesRows";

		public string Description
			=>
				"Rows of the expression matrix are filtered to contain at least the specified numbers of entries that are " +
				"valid in the specified way.";

		public string HelpOutput
			=> "The matrix of expression values is constrained to contain only these rows that fulfil the requirement.";

		public void ProcessData(IMatrixData mdata, Parameters param, ref IMatrixData[] supplTables,
			ref IDocumentData[] documents, ProcessInfo processInfo){
			const bool rows = true;
			int minValids = param.GetParam<int>("Min. number of values").Value;
			ParameterWithSubParams<int> modeParam = param.GetParamWithSubParams<int>("Mode");
			int modeInd = modeParam.Value;
			if (modeInd != 0 && mdata.CategoryRowNames.Count == 0){
				processInfo.ErrString = "No grouping is defined.";
				return;
			}
			FilteringMode filterMode;
			double threshold;
			double threshold2;
			PerseusPluginUtils.ReadValuesShouldBeParams(param, out filterMode, out threshold, out threshold2);
			if (modeInd != 0){
				int gind = modeParam.GetSubParameters().GetParam<int>("Grouping").Value;
				string[][] groupCol = mdata.GetCategoryRowAt(gind);
				NonzeroFilterGroup(minValids, mdata, param, modeInd == 2, threshold, threshold2, filterMode, groupCol);
			} else{
				NonzeroFilter1(rows, minValids, mdata, param, threshold, threshold2, filterMode);
			}
		}

		private static void NonzeroFilterGroup(int minValids, IMatrixData mdata, Parameters param, bool oneGroup,
			double threshold, double threshold2, FilteringMode filterMode, IList<string[]> groupCol){
			List<int> valids = new List<int>();
			string[] groupVals = ArrayUtils.UniqueValuesPreserveOrder(groupCol);
			Array.Sort(groupVals);
			int[][] groupInds = CalcGroupInds(groupVals, groupCol);
			for (int i = 0; i < mdata.RowCount; i++){
				int[] counts = new int[groupVals.Length];
				for (int j = 0; j < mdata.ColumnCount; j++){
					if (PerseusPluginUtils.IsValid(mdata.Values[i, j], threshold, threshold2, filterMode)){
						for (int k = 0; k < groupInds[j].Length; k++){
							if (groupInds[j][k] >= 0){
								counts[groupInds[j][k]]++;
							}
						}
					}
				}
				if ((oneGroup ? ArrayUtils.Max(counts) : ArrayUtils.Min(counts)) >= minValids){
					valids.Add(i);
				}
			}
			PerseusPluginUtils.FilterRows(mdata, param, valids.ToArray());
		}

		internal static int[][] CalcGroupInds(string[] groupVals, IList<string[]> groupCol){
			int[][] result = new int[groupCol.Count][];
			for (int i = 0; i < result.Length; i++){
				result[i] = new int[groupCol[i].Length];
				for (int j = 0; j < result[i].Length; j++){
					result[i][j] = Array.BinarySearch(groupVals, groupCol[i][j]);
				}
			}
			return result;
		}

		private static void NonzeroFilter1(bool rows, int minValids, IMatrixData mdata, Parameters param, double threshold,
			double threshold2, FilteringMode filterMode){
			if (rows){
				List<int> valids = new List<int>();
				for (int i = 0; i < mdata.RowCount; i++){
					int count = 0;
					for (int j = 0; j < mdata.ColumnCount; j++){
						if (PerseusPluginUtils.IsValid(mdata.Values[i, j], threshold, threshold2, filterMode)){
							count++;
						}
					}
					if (count >= minValids){
						valids.Add(i);
					}
				}
				PerseusPluginUtils.FilterRows(mdata, param, valids.ToArray());
			} else{
				List<int> valids = new List<int>();
				for (int j = 0; j < mdata.ColumnCount; j++){
					int count = 0;
					for (int i = 0; i < mdata.RowCount; i++){
						if (PerseusPluginUtils.IsValid(mdata.Values[i, j], threshold, threshold2, filterMode)){
							count++;
						}
					}
					if (count >= minValids){
						valids.Add(j);
					}
				}
				PerseusPluginUtils.FilterColumns(mdata, param, valids.ToArray());
			}
		}

		public Parameters GetParameters(IMatrixData mdata, ref string errorString){
			Parameters[] subParams = new Parameters[3];
			subParams[0] = new Parameters(new Parameter[0]);
			subParams[1] = new Parameters(new Parameter[]{new SingleChoiceParam("Grouping"){Values = mdata.CategoryRowNames}});
			subParams[2] = new Parameters(new Parameter[]{new SingleChoiceParam("Grouping"){Values = mdata.CategoryRowNames}});
			return
				new Parameters(new Parameter[]{
					new IntParam("Min. number of values", 3){
						Help = "If a row/column has less than the specified number of valid values it will be discarded in the output."
					},
					new SingleChoiceWithSubParams("Mode"){
						Values = new[]{"In total", "In each group", "In at least one group"},
						SubParams = subParams
					},
					PerseusPluginUtils.GetValuesShouldBeParam(), PerseusPluginUtils.GetFilterModeParam(false)
				});
		}
	}
}