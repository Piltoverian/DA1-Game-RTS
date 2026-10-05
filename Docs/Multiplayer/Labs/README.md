# Các lab MapGen

- [Lab5](Lab5/Lab5_README.md): terrain, occupancy, navigation, resource và regression tests.
- [Lab6](Lab6/Lab6_README.md): render plan, atlas, công cụ gen/render và kiểm tra texture.
- [Lab5_6](Lab5_6/Lab5_6_Combined_Map_Texture.html): HTML kết hợp hiện hành, app, builder và test preset 256×256 full map.

```powershell
node Docs/Multiplayer/Labs/Lab5_6/build-lab56.cjs
node Docs/Multiplayer/Labs/Lab5_6/lab56-256-tests.cjs
node Docs/Multiplayer/Labs/Lab6/lab6-generate.cjs 30000
pwsh -File Docs/Multiplayer/Labs/Lab6/render-lab6-full.ps1
```

Các lab đầu và bản thử cũ giữ riêng trong [archive](../Archive/2026-10-04/README.md).
