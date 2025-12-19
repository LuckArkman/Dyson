using System.Data;
using System.Text.RegularExpressions;
using Mono.Data.Sqlite;

namespace Core;

public class VocabularyManager : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private SqliteConnection _connection;

        // Cache L1 para tokens frequentes
        private readonly Dictionary<string, int> _hotCacheWordToId;
        private readonly Dictionary<int, string> _hotCacheIdToWord;
        private const int CACHE_SIZE = 1000;

        public VocabularyManager()
        {
            _dbPath = Path.Combine(Environment.CurrentDirectory, "Dayson", "vocab.db");
            _connectionString = $"Data Source={_dbPath}";
            
            _hotCacheWordToId = new Dictionary<string, int>();
            _hotCacheIdToWord = new Dictionary<int, string>();

            var dir = Path.GetDirectoryName(_dbPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            InitializeConnection();
        }

        private void InitializeConnection()
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL; 
                PRAGMA synchronous = NORMAL;
            ";
            cmd.ExecuteNonQuery();
            
            CreateSchema();
        }

        private void CreateSchema()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS vocab (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    text TEXT NOT NULL UNIQUE COLLATE NOCASE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS idx_vocab_text ON vocab(text);
            ";
            cmd.ExecuteNonQuery();
        }

        public int BuildVocabulary(string datasetPath)
        {
            Console.WriteLine($"[VocabularyManager] Atualizando vocabulário a partir de: {datasetPath}");
            
            if (!File.Exists(datasetPath))
                throw new FileNotFoundException("Dataset não encontrado.", datasetPath);

            // 1. Contagem de Frequência (Streaming)
            var tempCounts = new Dictionary<string, int>();
            
            using (var reader = new StreamReader(datasetPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var tokens = Regex.Split(line.ToLower(), @"(\p{L}+|\p{N}+|[.,!?;:'""/\-])")
                                      .Where(x => !string.IsNullOrWhiteSpace(x));
                    
                    foreach (var token in tokens)
                    {
                        if (!tempCounts.TryAdd(token, 1))
                            tempCounts[token]++;
                    }
                }
            }

            // 2. DEFINIÇÃO DE SORTED TOKENS (AQUI ESTAVA O ERRO)
            // Seleciona os tokens mais frequentes para garantir qualidade do vocabulário
            var sortedTokens = tempCounts
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .ToList();

            // Limpa a memória da contagem
            tempCounts.Clear();
            GC.Collect();

            // 3. Inserção no SQLite (Auto-incremento)
            using (var transaction = _connection.BeginTransaction())
            {
                // --- Passo A: Garantir Tokens Especiais ---
                var cmdSpecial = _connection.CreateCommand();
                cmdSpecial.Transaction = transaction;
                cmdSpecial.CommandText = "INSERT OR IGNORE INTO vocab (id, text) VALUES (@id, @text)";
                
                cmdSpecial.Parameters.AddWithValue("@id", 0);
                cmdSpecial.Parameters.AddWithValue("@text", "<PAD>");
                cmdSpecial.ExecuteNonQuery();

                cmdSpecial.Parameters["@id"].Value = 1;
                cmdSpecial.Parameters["@text"].Value = "<UNK>";
                cmdSpecial.ExecuteNonQuery();

                // --- Passo B: Inserir Tokens do Dataset ---
                var cmdInsert = _connection.CreateCommand();
                cmdInsert.Transaction = transaction;
                cmdInsert.CommandText = "INSERT OR IGNORE INTO vocab (text) VALUES (@text)";
                
                var pText = cmdInsert.CreateParameter();
                pText.ParameterName = "@text";
                cmdInsert.Parameters.Add(pText);

                // Loop usando a variável sortedTokens definida acima
                foreach (var token in sortedTokens)
                {
                    if (token == "<pad>" || token == "<unk>") continue;

                    pText.Value = token;
                    cmdInsert.ExecuteNonQuery();
                }

                transaction.Commit();
            }

            LoadHotCache();
            int total = GetVocabCount();
            Console.WriteLine($"[VocabularyManager] Vocabulário atualizado. Total de tokens: {total}");
            return total;
        }

        // --- Métodos de Compatibilidade e Acesso ---

        public int VocabSize => GetVocabCount();

        public int LoadVocabulary()
        {
            if (_connection.State != ConnectionState.Open) InitializeConnection();
            LoadHotCache();
            return VocabSize;
        }

        public int GetTokenIndex(string token)
        {
            if (_hotCacheWordToId.TryGetValue(token, out int id)) return id;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT id FROM vocab WHERE text = @text LIMIT 1";
            cmd.Parameters.AddWithValue("@text", token);

            var result = cmd.ExecuteScalar();
            if (result != null) return Convert.ToInt32(result);

            return _hotCacheWordToId.TryGetValue("<UNK>", out int unk) ? unk : 1;
        }

        public string GetToken(int id)
        {
            if (_hotCacheIdToWord.TryGetValue(id, out string token)) return token;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT text FROM vocab WHERE id = @id LIMIT 1";
            cmd.Parameters.AddWithValue("@id", id);

            var result = cmd.ExecuteScalar();
            return result != null ? result.ToString() : "<UNK>";
        }

        private void LoadHotCache()
        {
            _hotCacheWordToId.Clear();
            _hotCacheIdToWord.Clear();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = $"SELECT id, text FROM vocab ORDER BY id ASC LIMIT {CACHE_SIZE}";
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                string text = reader.GetString(1);
                _hotCacheWordToId[text] = id;
                _hotCacheIdToWord[id] = text;
            }
        }

        private int GetVocabCount()
        {
            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM vocab";
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch { return 0; }
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }