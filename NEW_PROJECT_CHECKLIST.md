# New Project Setup Checklist

Use this checklist when starting a new project based on this template.

## 1. Copy Template Files

```bash
# Option A: Clone template and rename
git clone https://github.com/{{AUTHOR}}/project-template.git MyNewProject
cd MyNewProject
rm -rf .git

# Option B: Copy folder manually
xcopy /E /I project-template\ MyNewProject\
```

Files to copy:
- [ ] `.gitignore`
- [ ] `ARCHITECTURE.template.md`
- [ ] `CHANGELOG.template.md`
- [ ] `CONTRIBUTING.md`
- [ ] `DEVELOPMENT.template.md`
- [ ] `LICENSE` (rename to `LICENSE` — no template extension)
- [ ] `NEW_PROJECT_CHECKLIST.md`
- [ ] `PLAN.template.md`
- [ ] `README.template.md` → rename to `README.md`
- [ ] `.github/FUNDING.yml`
- [ ] `.github/PULL_REQUEST_TEMPLATE.md`
- [ ] `.github/dependabot.yml`
- [ ] `.github/ISSUE_TEMPLATE/bug_report.md`
- [ ] `.github/ISSUE_TEMPLATE/feature_request.md`
- [ ] `.github/workflows/release.yml`
- [ ] `docs/index.template.html`

## 2. Rename Template Files

- [ ] `README.template.md` → `README.md`
- [ ] `DEVELOPMENT.template.md` → `DEVELOPMENT.md`
- [ ] `ARCHITECTURE.template.md` → `ARCHITECTURE.md`
      *(Separar solo si DEVELOPMENT.md proyecta superar ~500 líneas con contenido de arquitectura
      mezclado. Para proyectos chicos, un solo DEVELOPMENT.md con secciones bien delimitadas alcanza.)*
- [ ] `PLAN.template.md` → `PLAN.md`
- [ ] `CHANGELOG.template.md` → `CHANGELOG.md`
- [ ] `docs/index.template.html` → `index.html` (in `docs/` folder)

## 3. Replace Placeholders

Search and replace all `{{VARIABLE}}` occurrences across all files:

### Global placeholders (all files)
| Placeholder | Replace with | Example |
|---|---|---|
| `{{PROJECT_NAME}}` | Project name | `SteamManager` |
| `{{AUTHOR}}` | GitHub username | `myusername` |
| `{{YEAR}}` | Current year | `2026` |
| `{{VERSION}}` | Project version | `0.1.0` |
| `{{DESCRIPTION}}` | Short project description | `A Steam management tool` |

### release.yml only
| Placeholder | Replace with | Example |
|---|---|---|
| `{{SOLUTION}}` | Solution file name | `SteamManager` (not `.slnx`) |
| `{{PROJECT_PATH}}` | Path to .csproj | `src/SteamManager` |
| `{{DOTNET_VERSION}}` | .NET version | `10` |
| `{{CODEQL_LANGUAGE}}` | CodeQL language | `csharp` |
| `{{RUNTIME_IDENTIFIER}}` | Runtime ID | `win-x64` |

### FUNDING.yml only
| Placeholder | Replace with | Action |
|---|---|---|
| `{{KOFI_USERNAME}}` | Ko-fi username | **If you don't use Ko-fi:** delete the Ko-fi section, keep only GitHub Sponsors block |
| `{{AUTHOR}}` | GitHub username | `myusername` |

### dependabot.yml only
| Placeholder | Replace with | Action |
|---|---|---|
| `{{AUTHOR}}` | GitHub username | Your GitHub username |
| Microsoft.* ignore | Review | **If you want to update all Microsoft packages:** delete the entire `ignore:` block |

## 4. Create Required Files

- [ ] `CHANGELOG.md` — copy header from `CHANGELOG.template.md`, remove all `{{VERSION}}` and `{{NEXT_VERSION}}` placeholders
- [ ] `docs/index.html` — copy from `docs/index.template.html`, replace all placeholders:
  - `{{PROJECT_NAME}}`
  - `{{VERSION}}`
  - `{{DESCRIPTION}}`
  - `{{FEATURES_LIST}}` — replace with `<li>` items or `<li>Coming soon</li>`
  - `{{GETTING_STARTED_CONTENT}}` — replace with installation/run instructions or `<p>Coming soon</p>`

## 5. GitHub Setup

### Enable Dependabot
1. Go to your repo on GitHub → **Settings** → **Code security and analysis**
2. Find **Dependabot** and click **Enable**
3. That's it — `dependabot.yml` will be picked up automatically

### Enable Required GitHub Actions Permissions
1. Go to your repo → **Settings** → **Actions** → **General**
2. Under **Workflow permissions**, select **Read and write permissions**
3. Save

### Add GitHub Topics (optional but recommended)
1. Go to your repo → **About** (top-right gear icon)
2. Add topics: `dotnet`, `wpf`, `steam`, `csharp`

## 6. Initial Verification

Run these commands locally:

```bash
dotnet build -c Release
dotnet test -c Release
```

- [ ] Solution builds without errors
- [ ] All tests pass

## 7. Initial Commit

```bash
git init
git add .
git commit -m "chore: initial project setup from project-template"
git remote add origin https://github.com/{{AUTHOR}}/{{PROJECT_NAME}}.git
git push -u origin main
```

## 8. Post-First-Release Verification

After cutting your first release tag (`git tag v0.1.0 && git push --tags`):

- [ ] Verify `release.yml` triggered on GitHub Actions
- [ ] Verify NuGet audit job ran (check Actions logs)
- [ ] Verify CodeQL analysis ran (Security tab)
- [ ] Verify GitHub Release was created

---

## Quick Reference: Where Each Placeholder Appears

```
{{PROJECT_NAME}}     → README.md, DEVELOPMENT.md, ARCHITECTURE.md,
                      PLAN.md, CHANGELOG.md, CONTRIBUTING.md,
                      release.yml, FUNDING.yml, docs/index.template.html
{{AUTHOR}}          → DEVELOPMENT.md, release.yml, dependabot.yml,
                      FUNDING.yml, NEW_PROJECT_CHECKLIST.md
{{YEAR}}            → LICENSE
{{VERSION}}         → CHANGELOG.md, docs/index.template.html
{{DESCRIPTION}}     → docs/index.template.html
{{SOLUTION}}        → release.yml
{{PROJECT_PATH}}    → release.yml
{{DOTNET_VERSION}}  → release.yml
{{CODEQL_LANGUAGE}} → release.yml
{{RUNTIME_IDENTIFIER}} → release.yml
{{KOFI_USERNAME}}   → FUNDING.yml
```

---

*Delete this file after setting up a new project (or keep as reference).*
