using BasicRegionNavigation.Models;
using MyDatabase;
using ClosedXML.Excel;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Services
{
    public interface IQueryService
    {
        Task<List<ProductionRecord>> GetProductsAsync(ProductQueryFilter filter);
        Task ExportToExcelAsync(IEnumerable<ProductionRecord> data, string filePath);
    }

    public class QueryService : IQueryService
    {
        private readonly IRepository<ProductionRecord> _repo;

        public QueryService(IRepository<ProductionRecord> repo)
        {
            _repo = repo;
        }

        public async Task<List<ProductionRecord>> GetProductsAsync(ProductQueryFilter filter)
        {
            var exp = Expressionable.Create<ProductionRecord>();

            // 1. 时间范围
            exp.And(x => x.CreateTime >= filter.StartTime && x.CreateTime <= filter.EndTime);

            // 2. 动态筛选
            if (!string.IsNullOrEmpty(filter.ProductCode))
                exp.And(x => x.ProductCode.Contains(filter.ProductCode));

            if (!string.IsNullOrEmpty(filter.ProjectNo))
                exp.And(x => x.ProjectNumber.Contains(filter.ProjectNo));

            // 注意：这里用 FixtureCode 对应 批次/挂具码
            if (!string.IsNullOrEmpty(filter.HangerCode))
                exp.And(x => x.FixtureCode.Contains(filter.HangerCode));

            // 注意：这里用 ProductColor 对应 颜色
            if (!string.IsNullOrEmpty(filter.Color))
                exp.And(x => x.ProductColor.Contains(filter.Color));

            if (!string.IsNullOrEmpty(filter.MaterialCategory))
                exp.And(x => x.ProductCategory.Contains(filter.MaterialCategory));

            if (!string.IsNullOrEmpty(filter.UpHangerModule))
                exp.And(x => x.UpLoadDeivceName.Contains(filter.UpHangerModule));

            // 3. [修复] 获取列表并排序
            // 你的 IRepository.GetListAsync 只有一个参数，所以先获取，再用 LINQ 排序
            var list = await _repo.GetListAsync(exp.ToExpression());

            // 在内存中按创建时间倒序
            return list.OrderByDescending(x => x.CreateTime).ToList();
        }
        public async Task ExportToExcelAsync(IEnumerable<ProductionRecord> data, string filePath)
        {
            await Task.Run(() =>
            {
                using (var workbook = new XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add("产品明细");

                    // [变更] Excel 表头调整：
                    // 1. 移除了 "产品位置"
                    // 2. 增加了 "颜色"
                    // 3. "挂具码" 改为 "批次"
                    string[] headers = {
                        "ID", "产品码", "项目号", "原材料别", "批次", // FixtureCode
                        "颜色", "上料机", "上供料时间", "上挂时间", "创建时间"
                    };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        sheet.Cell(1, i + 1).Value = headers[i];
                        sheet.Cell(1, i + 1).Style.Font.Bold = true;
                        sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    int row = 2;
                    foreach (var item in data)
                    {
                        int col = 1;
                        sheet.Cell(row, col++).Value = item.Id;
                        sheet.Cell(row, col++).Value = item.ProductCode;
                        sheet.Cell(row, col++).Value = item.ProjectNumber;
                        sheet.Cell(row, col++).Value = item.ProductCategory;
                        sheet.Cell(row, col++).Value = item.FixtureCode; // 批次
                        sheet.Cell(row, col++).Value = item.ProductColor; // [新增] 颜色
                        sheet.Cell(row, col++).Value = item.UpLoadDeivceName;

                        // 时间处理：空值不显示
                        sheet.Cell(row, col++).Value = item.UpLoad_Time;
                        sheet.Cell(row, col++).Value = item.UpperHangFlip_Time;
                        sheet.Cell(row, col++).Value = item.CreateTime;

                        row++;
                    }

                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(filePath);
                }
            });
        }
    }
}