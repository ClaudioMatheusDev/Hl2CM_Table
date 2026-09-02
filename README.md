# Hl2CM_Table

Trainer (cheat menu) para **Half-Life 2** feito em C#/WinForms, portado a partir de uma
cheat table do Cheat Engine (`hl2.txt`) para a Steam build **19307283** do jogo
(`server.dll`). Uso **offline / singleplayer**, para fins de estudo de engenharia
reversa e manipulação de memória de processo.

> ⚠️ Isso não passa em nenhum anti-cheat/VAC. Use só offline, singleplayer.

## O que ele faz

A cheat table original usa duas técnicas do Cheat Engine que este projeto reimplementa
manualmente via WinAPI (`ReadProcessMemory`, `WriteProcessMemory`, `VirtualAllocEx`):

1. **Captura de ponteiro por injeção de código ("code cave")** — a struct do jogador só
   existe num registrador (`EBX`) durante a execução de uma instrução específica do
   `server.dll`. O trainer sobrescreve essa instrução com um `jmp` para um pequeno bloco
   de código alocado na memória do processo do jogo, que salva `EBX` (e, para o pente de
   munição atual, `ESI`) num endereço fixo antes de devolver a execução ao ponto original.
2. **Patch de valor fixo** — para vida/munição infinitas, o trainer redireciona a
   instrução que aplicaria o dano/consumo e força um valor constante (`99` ou `999`) antes
   de continuar o fluxo normal do jogo.

Antes de aplicar qualquer patch, o trainer **confere os bytes originais** no processo. Se
não baterem com o que a tabela espera (ex.: o jogo foi atualizado para outra build), ele
recusa o patch e mostra um erro — em vez de arriscar corromper a memória e travar o jogo.

## Requisitos

- Windows
- [.NET SDK 8 ou superior](https://dotnet.microsoft.com/download) (o projeto usa `net10.0-windows`)
- Half-Life 2 (Steam), rodando **offline / singleplayer**, build 19307283

## Como rodar

```powershell
cd Hl2CM_Table
dotnet run --project src/Hl2CM.Trainer
```

Ou abra `Hl2CM_Table.sln` no Visual Studio e rode o projeto `Hl2CM.Trainer`.

Se ao clicar em **Conectar** aparecer erro de acesso negado, feche e rode o trainer
**como Administrador** (clique direito → Executar como administrador, ou rode o
terminal como admin antes do `dotnet run`).

## Como usar

1. Abra o Half-Life 2 e comece/carregue uma partida (é preciso estar dentro do jogo,
   controlando o personagem, não no menu principal).
2. Abra o trainer.
3. No campo **Processo**, escolha o jogo na lista (o combo já lista os processos com
   janela aberta e pré-seleciona `hl2` se ele já estiver rodando; clique em
   **Atualizar** se tiver aberto o jogo depois do trainer) e clique em **Conectar**.
   - `Conectado: hl2 (pid ...)` em verde = ok, hooks de captura de ponteiro instalados.
   - Se aparecer um erro de "bytes não batem / versão do jogo diferente", significa que
   o `server.dll` instalado não é a Steam build 19307283 que a tabela original mira — os
     offsets não são válidos para essa versão.
4. Marque os checkboxes em **Cheats** para ativar/desativar cada patch:
   - **Vida infinita**
   - **Armadura (Suit) infinita**
   - **Munição infinita (arma primária)**
   - **Munição infinita (arma secundária)**
5. Na lista de **Stats do jogador**:
   - Os campos são atualizados automaticamente a cada ~300ms com o valor atual lido do
     jogo (a atualização pausa enquanto você está digitando no campo).
   - Digite um valor e clique em **Definir** para escrever aquele valor na memória do
     jogo imediatamente (ex.: setar vida para 1, ou encher a munição de uma arma).
6. Ao fechar o trainer (ou desmarcar um checkbox), os bytes originais do `server.dll`
   são restaurados automaticamente — o jogo volta ao comportamento normal.

## Estrutura do projeto

```text
src/Hl2CM.Trainer/
├── Native/NativeMethods.cs   P/Invoke: OpenProcess, ReadProcessMemory,
│                             WriteProcessMemory, VirtualAllocEx/FreeEx
├── Memory/
│   ├── ProcessMemory.cs      Attach ao processo + leitura/escrita tipada
│   │                         (int, float, ponteiros, bytes)
│   ├── PatternScanner.cs     Confere bytes originais antes de qualquer patch
│   ├── X86Asm.cs             Monta bytes de instruções x86 (jmp rel32,
│   │                         mov [disp32],reg, mov reg,imm32)
│   └── CodeCave.cs           Equivalente ao bloco [ENABLE]/[DISABLE] da CT:
│                             aloca o cave, aplica o jmp, restaura ao desativar
├── Game/
│   ├── PlayerOffsets.cs      Offsets nomeados da struct do jogador (extraídos
│   │                         da CT table original)
│   └── Hl2Trainer.cs         Porta 1:1 das entradas da cheat table original:
│                             2 hooks de captura de ponteiro + 4 cheats de
│                             valor fixo + leitura/escrita de stats
└── Form1.cs                  Interface (conectar, checkboxes de cheat,
                               campos de stats)
```

## Offsets disponíveis (não ligados na interface)

`Game/PlayerOffsets.cs` já traz offsets extras da tabela original que não estão
expostos na UI (por serem mais arriscados de mexer sem cuidado ou de propósito incerto
no arquivo original):

- Posição (`PositionA/B/Vert`, `PositionAEditable/BEditable/VertEditable`) e velocidade
  (`Speed1DirA/DirB/Vert`, `Speed2DirA/DirB/Vert`) — escrever floats errados aqui pode
  teleportar o jogador para fora do mapa.
- Câmera (`CameraVert/Horiz`).
- Timers (`TimerInGame`, `TimerGlobal`, `TimerGlobal2`).

Para usar algum desses, adicione uma chamada a `AddIntStat`/um novo helper de float em
`Form1.cs` apontando para o offset desejado, seguindo o mesmo padrão dos campos de
munição já existentes.

## Referência original

O arquivo `hl2.txt` na raiz do repositório é a cheat table original do Cheat Engine da
qual todos os offsets e trechos de assembly foram extraídos.
