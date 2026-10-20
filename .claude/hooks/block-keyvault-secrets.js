// PreToolUse hook (Bash|PowerShell): refuse any command that reads, lists or
// enumerates Azure Key Vault secrets. Names and value lengths count as reading.
const patterns = [
  /\baz\s+keyvault\s+secret\b/i,
  /\bGet-AzKeyVaultSecret\b/i,
  /\bGet-Secret\b.*\bVault\b/i,
  /\.vault\.azure\.net\/secrets\b/i,
  /\bSecretClient\b/,
];

let input = "";
process.stdin.on("data", chunk => (input += chunk));
process.stdin.on("end", () => {
  const command = JSON.parse(input).tool_input?.command ?? "";
  if (!patterns.some(p => p.test(command))) return;

  process.stdout.write(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "deny",
      permissionDecisionReason:
        "Never read, list or enumerate Azure Key Vault secrets: no `az keyvault secret show`/`list`, " +
        "no `SecretClient`, no portal scraping, and not even secret names or value lengths. " +
        "If a task needs a secret value, ask the user to supply it or to run the command themselves.",
    },
  }));
});
