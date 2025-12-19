using Brain;
using Core;
using Interfaces;

namespace Brain;

public class ModelSerializerLSTM
    {
        /// <summary>
        /// Salva o modelo no caminho especificado, delegando a lógica para o próprio modelo.
        /// </summary>
        public static void SaveModel(GenerativeNeuralNetworkLSTM model, string filePath)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }
            
            model.SaveModel(filePath);
        }

        /// <summary>
        /// Carrega um modelo generativo do disco.
        /// </summary>
        public static GenerativeNeuralNetworkLSTM? LoadModel(string filePath, IMathEngine mathEngine)
        {
            try
            {
                // 1. Inicializa o VocabularyManager (Conecta ao SQLite)
                var vocabManager = new VocabularyManager();
                
                // O método LoadVocabulary agora carrega o HotCache e retorna a contagem do banco
                int vocabSize = vocabManager.LoadVocabulary();
                
                if (vocabSize == 0)
                {
                    Console.WriteLine("Erro: Vocabulário vazio. O banco de dados 'vocab.db' não foi encontrado ou está vazio. O carregamento do modelo não pode continuar.");
                    return null;
                }

                // 2. Delega a lógica de carregamento do modelo
                var searchService = new MockSearchService(); 
                
                // Passamos o vocabManager já instanciado e conectado
                var model = GenerativeNeuralNetworkLSTM.Load(filePath, mathEngine, vocabManager, searchService);

                if (model == null)
                {
                    return null;
                }

                // 3. Validação final de consistência entre vocabulário (DB) e modelo (JSON/Pesos)
                // vocabManager.VocabSize faz uma query SELECT COUNT(*) rápida no SQLite
                if (vocabManager.VocabSize != model.OutputSize)
                {
                    Console.WriteLine(
                        $"Erro de Inconsistência: Tamanho do vocabulário no banco ({vocabManager.VocabSize}) não corresponde ao OutputSize definido no modelo ({model.OutputSize}).");
                    return null;
                }
                
                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro crítico ao carregar o modelo LSTM generativo: {ex.Message}");
                return null;
            }
        }
    }