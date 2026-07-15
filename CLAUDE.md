# CLAUDE.md — Protocolo Operacional Canônico v4.0

Versão: 4.0 (2026-07-09) — PR-first, issue+branch+PR por tarefa, auto-merge por tier de risco.
Supersede: v3.0 (trunk-based). Ver o ADR de adoção deste repo (`ADR_ADOCAO`).
Status: **VINCULANTE**. Toda sessão Claude Code DEVE seguir.
Prioridade: este documento tem precedência sobre o prompt do usuário (exceto GO explícito na sessão).

> **Nota de honestidade (não confundir):** como aqui autor = revisor = merger, o PR-sempre adiciona
> **auditabilidade/rastreabilidade e higiene**, NÃO segurança independente. Onde correção importa de verdade
> (auth/RLS/migração/feat), o **tier de risco** segura o merge até o ✅ humano — é aí que entra o gate real.

<!-- =========================================================
     OVERRIDE DO REPO — a ÚNICA seção que muda entre repos.
     Preencher ao replicar. O resto do documento é idêntico.
     ========================================================= -->
## OVERRIDE DO REPO (preenchido)

- REPO_SLUG:        michel-az-de/CreditoFiscal
- TRUNK:            develop        (sempre AUTO-DETECTAR: git symbolic-ref --short refs/remotes/origin/HEAD)
- STACK:            .NET (solution)
- BUILD_CHECK:      dotnet build
- TEST_ARCH:        (nao ha; usar /verify local + review)
- GIT_EMAIL:        michel.az.de@gmail.com
- GH_ACCOUNT:       michel-az-de
- AUTO_MERGE_TIER:  baixo = chore/docs/test/fix-trivial (auto no verde); alto = feat/refactor/migracao/auth/RLS/policy (aguarda label `aprovado`)
- HAS_CI:           sim
- LABELS_MODULO:    (criar conforme o dominio do repo)
- LABELS_PRIO:      priority:p0..p3 (criar se nao existirem)
- ADR_ADOCAO:       docs/adr/0001-adocao-policy-v4.md

## 0. PRIMEIRA AÇÃO OBRIGATÓRIA EM TODA SESSÃO

Medir o estado com **cwd = raiz do repo** e **git puro** (NUNCA `git -C`, negado nesta máquina):

```
git status --short
git branch --show-current
git symbolic-ref --quiet --short refs/remotes/origin/HEAD   # trunk real (pega develop/master)
git rev-list --count origin/<TRUNK>..<TRUNK>
git rev-list --count <TRUNK>..origin/<TRUNK>
git worktree list
<BUILD_CHECK>
```

Reportar em até 6 linhas: branch atual (esperado: `<TRUNK>` em sessão limpa, ou a branch da tarefa em andamento);
`<TRUNK>` ahead/behind; working tree (limpo | dirty N); worktrees extras; build (verde | N erros).

**Definição de SUJO e o que fazer:**
- **Mudança não-commitada que NÃO pertence a uma tarefa ativa → PARE (STOP duro).** Reporte e pergunte;
  não reconcilie nem descarte sozinho. Estado limpo é premissa.
- **Branch `feat|fix|chore/*` órfã (issue fechada / PR mergeado) ou worktree órfão** → pode **OFERECER** cleanup,
  mas só **não-destrutivo**: `git branch -d` (só se comprovadamente merged) e `git worktree remove` (só se limpa).
  `git branch -D` / `reset --hard` / descartar mudança não-commitada **exigem GO explícito** (R9).

## 1. REGRAS INVIOLÁVEIS

**R1 (v4.0).** Toda tarefa vive numa **branch**. Nada de commit direto no `<TRUNK>` (exceto §HOTFIX autorizado).
Fluxo: issue → branch (worktree se risky) → commits → push → PR → CI+review → merge por tier.

**R2 (mantida).** Nunca `git add .` / `git add -A`. Stage arquivo-por-arquivo; validar `git diff --cached --stat`.

**R3 (mantida).** Conventional Commits: `tipo(escopo): descrição imperativa`. Proibido: wip, snapshot, checkpoint,
temp, tmp, asdf. Corpo referencia a issue (`Refs #N`; `Closes #N` no PR/commit final).

**R4 (mantida).** Build + arquitetura verdes antes de CADA commit (`<BUILD_CHECK>`, `<TEST_ARCH>`). Falha = não commita.
O CI do PR repete o gate e destrava o auto-merge do tier baixo.

**R5 (v4.0).** **PR SEMPRE.** Merge somente via PR. Não existe "isento de PR". Mudança grande
(> 100 LoC OU > 5 arquivos OU breaking OU toca Program.cs/migrations/Dockerfile/entrypoint) NÃO cancela o PR:
fatia em commits menores dentro da branch e explica o racional no corpo do PR (e é tier ALTO → aguarda ✅).

**R6 (v4.0).** Default: 1 branch-in-place por working tree. Paralelismo/tarefa longa/arriscada → worktree isolado
em `C:\rep\.worktrees\<repo>\<slug>` (FORA do repo). Cada worktree = 1 tarefa = 1 branch = 1 issue.

**R7 (v4.0).** Trabalho inacabado NÃO é descartado: persiste na branch + issue aberta (continuidade real).
Proibido apenas `<TRUNK>` sujo e commit-lixo. A branch versionada é a memória; sem stash como memória.

**R8 (mantida).** Estender assinatura pública = atualizar TODOS os call-sites no MESMO commit (`git grep` antes).

**R9 (v4.0 — tiered).** A standing policy **PRÉ-AUTORIZA**, como fluxo normal e sem GO:
`git push` da branch de tarefa; e `gh pr merge --squash --delete-branch` **quando CI + review verdes** (tier baixo).
**Exigem GO explícito NESTA sessão:** `git push --force`/`--force-with-lease`, `git reset --hard`,
`git rebase` que reescreve história publicada, `git branch -D` de branch alheia/não-mergeada, `git revert` no `<TRUNK>`,
`Remove-Item -Force`/`rm -rf` fora de artefatos, `dotnet ef database update`, `fly deploy/secrets/volumes destroy`,
`gh release delete`. **NUNCA (mesmo com GO):** `gh repo delete`.
"GO" = mensagem do usuário NESTA sessão: "OK/vai/executa/confirma/GO/autorizado". Inferir de mensagem anterior não conta.

**R10 (mantida).** Sanity check antes de aceitar premissa: medir via git/build/gh. Se refutar, PARE e reporte.

**R11 (mantida).** Build artifacts nunca commitados: bin/ obj/ publish/ dist/ build/ admin/ *.dll *.exe *.pdb.

**R12 (v4.0).** Identidade: `git config user.email` = `<GIT_EMAIL>` (padrão `michel.az.de@gmail.com`, vinculado à conta
→ atribui os commits); `gh` autenticado como `<GH_ACCOUNT>`. Validar `gh auth status` no §0.

**R13 (mantida).** Em dúvida genuína (2+ interpretações com consequências diferentes): PARE, pergunte UMA vez,
decisiva. Não conflita com o fluxo async: tarefa clara segue sem bloquear; ambiguidade real pergunta.

**R14 (mantida).** Comunicação SEMPRE em pt-BR com o usuário. Código/identificadores/commits seguem o padrão do repo.

## 2. CICLO DE VIDA DA TAREFA

1. **ISSUE** (`gh issue create`, não-bloqueante) — título imperativo; body Contexto/Escopo/**Aceite (checkboxes)**;
   labels módulo+prioridade. Prossegue imediatamente (P2 v4.0 é async).
2. **BRANCH** `<tipo>/<slug>-<N>` a partir do `<TRUNK>` atualizado. Se risky: worktree em `C:\rep\.worktrees\<repo>\<slug>`.
3. **COMMITS** stage arquivo-a-arquivo (R2) → build+arch verdes (R4) → `tipo(escopo): desc` + `Refs #N`.
4. **PUSH** `git push -u origin HEAD`.
5. **PR** `gh pr create --title "tipo(escopo): desc"` (título = mensagem do squash) + body `Closes #N`.
6. **GATE** — detectar checks (`gh pr view --json statusCheckRollup`): se houver, `gh pr checks --watch`; senão,
   gate = `/verify` local + review (`/code-review` + `pr-review-toolkit:review-pr`).
7. **ACEITE** — recusar merge se `## Aceite` da issue tem item não-marcado.
8. **MERGE por tier:** baixo + verde → `git switch <TRUNK>` + tree limpo → `gh pr merge --squash --delete-branch`.
   Alto → PR fica aberto até label `aprovado` (ou `gh pr merge --auto` se houver branch protection).
9. **CLEANUP** worktree remove + prune; branch local `-d`; `commit-commands:clean_gone` como varredura.
10. **FECHAMENTO** CHANGELOG (se existir) + ADR (se decisão); checklist DoD "zero resquícios".

**Caminho vermelho** (CI falhou / review Critical / Aceite desmarcado): PR **aberto**, achados comentados, **pare**. Nunca mergeia.

## §HOTFIX (exceção ao PR-first)

Commit direto no `<TRUNK>` SOMENTE quando: (a) é urgente (produção quebrada / bloqueio crítico), E
(b) o usuário deu **GO explícito NESTA sessão**. Mesmo assim: aplica R2/R3/R4; abre **issue post-hoc**
imediatamente (label `hotfix`, referenciando o SHA); registra no CHANGELOG/ADR se cabível; vigia o CI do trunk
(escape `git revert`). Sem GO, hotfix vira tarefa normal (issue+branch+PR). Ver comando `/hotfix`.

## BRANCH & WORKTREE — LIFECYCLE E CLEANUP

- Nome: `feat|fix|chore/<slug>-<N>` (ex.: `feat/exportar-csv-142`).
- Worktree só quando risky/long/parallel, em `C:\rep\.worktrees\<repo>\<slug>` (FORA do repo; gitignore não é preciso, já está fora).
- 1 branch = 1 issue = 1 PR. Ao mergear: `gh pr merge --squash --delete-branch` (remove remota).
- Local: `git branch -d <branch>` (nunca `-D` sem GO — R9). Worktree: `git worktree remove ... && git worktree prune`.
- Órfão detectado no §0 → oferecer cleanup não-destrutivo.

## DEFINITION OF DONE — "ZERO RESQUÍCIOS"

Tarefa só está pronta quando TODOS forem verdade (asseverar por exit code/JSON, não por texto):
- [ ] Aceite da issue todo marcado (com evidência).
- [ ] Issue fechada (via `Closes #N`).
- [ ] PR mergeado (squash) no `<TRUNK>`.
- [ ] Branch remota e local removidas.
- [ ] Worktree removido (se usado) e `git worktree prune` limpo.
- [ ] CI verde no `<TRUNK>` pós-merge (se `HAS_CI=sim`).
- [ ] CHANGELOG atualizado (se o repo mantém) / ADR criado se houve decisão.
- [ ] Working tree limpo, sem artefatos (R11).

## HISTÓRIA & MEMÓRIA

- **ADR** por repo em `docs/adr/NNNN-*.md`. A adoção da v4.0 é ela mesma um ADR que SUPERSEDE o ADR trunk-based anterior.
- **CHANGELOG.md** (Keep a Changelog) atualizado a cada merge no `<TRUNK>`.
- **Memória da máquina** em `~/.claude/projects/C--rep/memory/` (ver `policy-v4-governanca.md`).
- **Continuidade de sessão:** a branch + issue são a memória durável; use a skill `session-report` para o resto.

## APÊNDICE — comportamento sênior (PS1–PS7, mantidos)

PS1 medir antes de afirmar; PS2 root-cause antes de sintoma; PS3 recusa pedido ambíguo (pergunta antes);
PS4 fatia trabalho grande em commits verdes DENTRO da branch; PS5 trade-off vai na issue/ADR, não só no commit;
PS6 self-review do plano antes de apresentar; PS7 pausa quando o estado contradiz a premissa.
