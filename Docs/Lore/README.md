# Old Scars — Lore

Este directorio es la fuente organizada de documentación narrativa y de worldbuilding de **Old Scars**.

Su objetivo es separar el lore de la documentación técnica del juego y permitir registrar de forma trazable tanto contenido ya establecido como material todavía en exploración: historia, tecnología, facciones, personajes, lugares, culturas, conflictos, tramas, eventos e ideas.

## Convención de IDs

Cada entrada usa un identificador estable:

- `LORE-001`
- `LORE-002`
- `LORE-003`
- etc.

El ID no cambia aunque el título o el contenido del documento evolucionen.

Formato recomendado de archivo:

`LORE-XXX_Titulo_Descriptivo.md`

## Estados

Cada entrada debe indicar uno de estos estados:

- **CANON** — Parte establecida del universo. Puede ampliarse, pero no debe reinterpretarse como mera propuesta.
- **DRAFT** — Dirección en desarrollo. Tiene intención de incorporarse, pero todavía contiene decisiones abiertas.
- **IDEA** — Propuesta o posibilidad. No forma parte del canon hasta que se apruebe explícitamente.
- **RETIRED** — Concepto descartado o reemplazado. Se conserva por trazabilidad histórica.

Una entrada puede mezclar información establecida y cuestiones todavía abiertas, pero debe separarlas explícitamente bajo secciones como **Canon establecido** y **Preguntas abiertas**.

## Tipos de entrada

El campo `Tipo` puede usar una o varias categorías:

- Historia
- Mundo
- Tecnología
- Militar
- Facción
- Nación
- Personaje
- Lugar
- Cultura
- Organización
- Evento
- Trama
- Conflicto
- Criatura / fauna
- Objeto
- Idea general

Estas categorías sirven para organizar el contenido; no reemplazan el ID.

## Plantilla mínima

```md
# LORE-XXX — Título

- Estado: CANON | DRAFT | IDEA | RETIRED
- Tipo: ...
- Última revisión: YYYY-MM-DD

## Resumen

...

## Canon establecido

...

## Preguntas abiertas

...
```

## Índice

| ID | Título | Estado | Tipo |
|---|---|---|---|
| [LORE-001](./LORE-001_Industrializacion_Acero_Termico_y_Armaduras_Motorizadas.md) | Industrialización, acero térmico y armaduras motorizadas | CANON | Historia / Tecnología / Militar |

## Regla de uso

Registrar una idea en esta carpeta **no la convierte automáticamente en canon**. El estado del documento es la autoridad narrativa inmediata para distinguir contenido establecido de contenido exploratorio.

Cuando una decisión de lore afecte directamente diseño jugable, balance, arte o implementación, la documentación técnica correspondiente puede enlazar esta entrada, pero el detalle narrativo debe mantenerse aquí.
