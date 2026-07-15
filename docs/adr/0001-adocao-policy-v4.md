# ADR-0001 — Adocao da Policy v4.0 (PR-first, issue-driven)

- Status: Aceito
- Data: 2026-07-09
- Supersede: (primeira policy formal deste repo; nenhum ADR trunk-based anterior).

## Contexto

Os repos em `C:\rep` sao desenvolvidos por Claude Code. A politica anterior era trunk-based
(commit direto no trunk, sem PR, sem branches). A direcao passou a exigir: **PR sempre** (exceto
hotfix urgente autorizado), fluxo alinhado ao GitHub (issue -> PR -> close), limpeza de
branches/worktrees ao terminar, e historico/memoria para continuidade.

## Decisao

Adotar o **Protocolo Operacional Canonico v4.0** (ver `CLAUDE.md`): toda tarefa = issue + branch + PR,
com **auto-merge por tier de risco** (baixo = auto no verde; alto = aguarda `aprovado`), worktrees fora
do repo (`C:\rep\.worktrees`), e Definition of Done com criterio de Aceite verificavel.

## Consequencias

- Trunk deixa de receber commit direto (salvo hotfix autorizado com issue post-hoc).
- Ganha-se auditabilidade (revert granular, trilha), NAO seguranca independente (autor=revisor=merger);
  o gate real de correcao e o tier alto + ✅ humano.
- Identidade de commit passa a `michel.az.de@gmail.com` (vinculado a conta -> atribui no GitHub).
- Automacao via `/tarefa-inicio`, `/tarefa-fim`, `/hotfix` + hooks SessionStart/Stop.
- O Dev Janitor pula repos fora do trunk (guard) e nao toca `C:\rep\.worktrees`.

## Alternativas consideradas

- Manter trunk-based v3.0: rejeitada (nao atende PR-sempre nem a gestao por issue pedida).
- CLAUDE.md global unico: rejeitada (preferencia por politica por-repo com override).
- Auto-merge total sem tier: rejeitada (daria falsa sensacao de gate; alto risco sem ✅ humano).
