# Acceptance Criteria Matrix

| ID | Componente | Criterio de Aceptación | Método de Verificación |
|---|---|---|---|
| AC-1 | Ingesta | Debe leer eventos de auditoría de forma incremental sin reprocesar eventos previamente leídos. | Guardar marca de agua (`LastAuditTimestamp`) y verificar lectura en base a timestamp. |
| AC-2 | Baseline | Maneja el estado *cold-start*: Durante los primeros $N$ días o $M$ eventos, no dispara alertas de volumen, solo aprende. | Configurar `LearningWindowHours = 1` en tests y validar su transición a activo. |
| AC-3 | Detección | Ante deshabilitación de auditoría (`AUDIT_CHANGE`), emite alerta `CRIT` con Score = 100 independientemente del baseline. | Correr escenario 8 del script PowerShell. |
| AC-4 | De-duplicación| 50 accesos fallidos seguidos dentro de 90s deben generar 1 sola alerta de sesión con contador `(agrupados, x50)`. | Correr escenario 5 del script PowerShell. |