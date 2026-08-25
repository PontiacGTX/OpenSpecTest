# Feature Spec: Multi-Layer Detection Engine

## 1. Reglas Determinísticas (Weight: 40%)
- **AuditTamper:** Acción `AUDIT_CHANGE` o alteración de auditoría. Score = 100 fijo, Severidad = `CRIT`.
- **SensitiveSpExec:** Uso de `xp_cmdshell`, `sp_configure`, `OPENROWSET`. Score = 95 fijo, Severidad = `CRIT`.
- **UnknownHost:** Conexión desde un IP/Host fuera de la lista `knownHosts` del perfil. Score base = +25.
- **OffHours:** Acceso fuera de las horas habituales (+/- 3 desvíos estándar del horario común). Score base = +20.

## 2. Engine Estadístico (Weight: 40%)
- Mide la desviación de `RowsAffected` mediante $Z$-score:
  $$Z = \frac{X - \mu}{\sigma}$$
- Si $\sigma = 0$, se utiliza una varianza mínima por defecto para evitar división por cero.
- Mide la frecuencia mediante Promedio Ponderado Exponencial en el Tiempo (EWMA):
  $$S_t = \alpha Y_t + (1 - \alpha) S_{t-1}$$
- $Z > 3.0 \implies \text{Score} += 35$.

## 3. Engine Agéntico / Semántico (Semantic Kernel + Ollama) (Weight: 20%)
- Evalúa sentencias SQL dinámicas sospechosas que no cumplen patrones AST conocidos (e.g., inyecciones SQL evadidas, paginaciones evasivas `TOP 1` repetidas 10,000 veces).
- Retorna un nivel de sospecha [0.0 - 1.0].