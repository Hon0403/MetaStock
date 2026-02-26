using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using MetaStockSync;
using Npgsql;

/// <summary>
/// 使用 Npgsql 直接連線 PostgreSQL，取代 Supabase Client。
/// 核心方法: BatchSaveAsync (Upsert) 和 ExistsAsync。
/// </summary>
public class StockRepository
{
    private readonly string _connectionString;

    // 每張表的 Metadata: 表名、欄位映射、主鍵欄位
    private static readonly Dictionary<Type, TableMeta> _tableMetas = new()
    {
        [typeof(StockInfo)] = new("stocks",
            new[] { "stock_id" }),
        [typeof(DailyPrice)] = new("prices",
            new[] { "stock_id", "date" }),
        [typeof(InstitutionalTrade)] = new("institutional",
            new[] { "stock_id", "date" }),
        [typeof(MonthlyRevenue)] = new("revenues",
            new[] { "stock_id", "year", "month" }),
        [typeof(Shareholders)] = new("shareholders",
            new[] { "stock_id", "date" }),
        [typeof(Margins)] = new("margins",
            new[] { "stock_id", "date" }),
        [typeof(Valuation)] = new("valuations",
            new[] { "stock_id", "date" }),
        [typeof(DayTrade)] = new("daytrades",
            new[] { "stock_id", "date" }),
        [typeof(Dividend)] = new("dividends",
            new[] { "stock_id", "ex_date" }),
        [typeof(BrokerTrade)] = new("brokertrades",
            new[] { "stock_id", "date", "broker_id", "price" }),
        [typeof(Financial)] = new("financials",
            new[] { "stock_id", "year", "quarter" }),
    };

    public StockRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// 批次 Upsert — INSERT ... ON CONFLICT DO UPDATE
    /// </summary>
    public async Task BatchSaveAsync<T>(List<T> items, string dataTypeName)
    {
        if (items == null || items.Count == 0) return;
        Console.WriteLine($"正在同步 {items.Count} {dataTypeName}到資料庫...");

        if (!_tableMetas.TryGetValue(typeof(T), out var meta))
            throw new InvalidOperationException($"未定義表 Metadata: {typeof(T).Name}");

        // 取得所有有 [JsonPropertyName] 的屬性 → 資料庫欄位映射
        var props = GetColumnMappings(typeof(T));
        var columnNames = props.Select(p => p.ColumnName).ToList();
        var primaryKeys = meta.PrimaryKeys;
        var updateColumns = columnNames.Except(primaryKeys).ToList();

        // 分批寫入 (每批 500 筆，避免 SQL 太長)
        foreach (var batch in items.Chunk(500))
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var sb = new StringBuilder();
                sb.Append($"INSERT INTO {meta.TableName} ({string.Join(", ", columnNames)}) VALUES ");

                var parameters = new List<NpgsqlParameter>();
                int paramIndex = 0;

                for (int i = 0; i < batch.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('(');

                    for (int j = 0; j < props.Count; j++)
                    {
                        if (j > 0) sb.Append(", ");
                        var paramName = $"@p{paramIndex}";
                        sb.Append(paramName);

                        var value = props[j].Property.GetValue(batch[i]);
                        // DateTime 轉換為 DateOnly 或 UTC（PostgreSQL DATE 欄位需要）
                        if (value is DateTime dt && props[j].ColumnName != "created_at")
                        {
                            value = dt.Date; // 確保只取日期部分
                        }
                        parameters.Add(new NpgsqlParameter(paramName, value ?? DBNull.Value));
                        paramIndex++;
                    }
                    sb.Append(')');
                }

                // ON CONFLICT 更新非主鍵欄位
                sb.Append($" ON CONFLICT ({string.Join(", ", primaryKeys)}) DO UPDATE SET ");
                sb.Append(string.Join(", ", updateColumns.Select(c => $"{c} = EXCLUDED.{c}")));

                await using var cmd = new NpgsqlCommand(sb.ToString(), conn);
                cmd.Parameters.AddRange(parameters.ToArray());
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[錯誤] 儲存{dataTypeName}批次失敗: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 檢查資料是否存在 — SELECT EXISTS(...)
    /// 用物件的非預設值屬性作為查詢條件
    /// </summary>
    public async Task<bool> ExistsAsync<T>(T condition)
    {
        if (!_tableMetas.TryGetValue(typeof(T), out var meta))
            throw new InvalidOperationException($"未定義表 Metadata: {typeof(T).Name}");

        var props = GetColumnMappings(typeof(T));
        var whereClauses = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        int paramIndex = 0;

        // 用主鍵欄位建立 WHERE 條件
        foreach (var pk in meta.PrimaryKeys)
        {
            var prop = props.FirstOrDefault(p => p.ColumnName == pk);
            if (prop == null) continue;

            var value = prop.Property.GetValue(condition);
            if (value == null) continue;

            // 跳過預設值（string 的 null/empty, int 的 0, DateTime 的 MinValue）
            if (value is string s && string.IsNullOrEmpty(s)) continue;
            if (value is DateTime dt && dt == DateTime.MinValue) continue;

            var paramName = $"@p{paramIndex++}";
            whereClauses.Add($"{pk} = {paramName}");

            if (value is DateTime dateVal)
                parameters.Add(new NpgsqlParameter(paramName, dateVal.Date));
            else
                parameters.Add(new NpgsqlParameter(paramName, value));
        }

        if (whereClauses.Count == 0) return false;

        var sql = $"SELECT EXISTS(SELECT 1 FROM {meta.TableName} WHERE {string.Join(" AND ", whereClauses)} LIMIT 1)";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters.ToArray());

        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }

    /// <summary>
    /// 透過反射取得 [JsonPropertyName] 對應的資料庫欄位映射
    /// </summary>
    private static List<ColumnMapping> GetColumnMappings(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p =>
            {
                var attr = p.GetCustomAttribute<JsonPropertyNameAttribute>();
                if (attr == null) return null;
                return new ColumnMapping(attr.Name, p);
            })
            .Where(m => m != null)
            .ToList()!;
    }

    // 內部結構: 表的 Metadata
    private record TableMeta(string TableName, string[] PrimaryKeys);
    private record ColumnMapping(string ColumnName, PropertyInfo Property);
}
