# System Specification: DAM Anomaly Detection Engine

## 1. Propósito
El sistema procesa eventos de auditoría nativos de SQL Server para identificar comportamientos anómalos de acceso a datos sin basarse exclusivamente en datos etiquetados previos.

## 2. Bounded Contexts
1. **Ingestion Context:** Lee la tabla/archivo de auditoría nativa de forma incremental garantizando idempotencia mediante `event_time` y `sequence_number`.
2. **Baseline Context:** Mantiene la media y desviación estándar de patrones de volumen ($\mu$, $\sigma$), ventanas de horario y mapas de calor de objetos accesados por usuario. Administra la fase de *cold-start*.
3. **Detection Engine Context:** Combina reglas determinísticas, scoring estadístico ($Z$-score / EWMA) y evaluación semántica mediante Semantic Kernel / Ollama.
4. **Scoring & Alerting Context:** Agrupa hallazgos a nivel de sesión/ventana de tiempo, desduplica eventos repetitivos, calcula el score ponderado compuesto y gestiona el estado de alerta.

## 3. Principios de Arquitectura
- Hexagonal / Clean Architecture.
- Modelo de procesamiento asíncrono en batch deslizable.
- Persistencia separada para auditoría cruda vs. baselines/alertas.