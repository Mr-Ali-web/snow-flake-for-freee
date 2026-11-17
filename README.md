# SnowFlakeUz

Mac ish stolida qor yog'diradigan menu-bar ilova (Swift / AppKit). Hammasi original kod va original chizmalar.

## Ishga tushirish
```bash
cd SnowFlakeUz
./build.sh
open build/SnowFlakeUz.app
```
Xcode yoki Command Line Tools kerak (`xcode-select --install`). macOS 12+.
Yuqori menyu qatoridagi ❄ belgisidan hamma sozlamalar ochiladi.

## Imkoniyatlar
- Qor yog'adi, bosishlar qor orqali o'tib ketadi (click-through)
- Qor oynalar tepasida va ekran pastida (Dock ustida) to'planadi; boshqa oyna qoplagan joyda ko'rinmaydi
- Oynani sursangiz qor to'kiladi, oyna yopilsa/yig'ilsa qor pastga tushadi
- **Qor yog'ishi**: yengil / tekis / bo'ron. **Shamol**: chap/o'ng, kuchli/kuchsiz
- **Asbob → Qo'lqop**: kursorni qor ustida yurgizsangiz qor supuriladi
- **Asbob → Qor shari va qorbobo yasash**: qor uyumini bosib ushlab, uyum bo'ylab sudrang — shar qor yig'ib kattalashadi.
  Qo'yib yuborsangiz tushadi. Kichikroq sharni kattaroq shar ustiga tashlasangiz qorbobo yig'iladi
  (ko'zlar, sabzi burun, tugmalar, qo'llar va shlyapa o'zi chiziladi). Sharni yoki qorbobo boshini bosib qayta ko'tarish mumkin.
  Qorbobo oyna bilan birga siljiydi. (Bu rejimda faqat qor uyumi/shar ustida bosish ushlanadi, qolgan joyda bosishlar o'tib ketadi.)
- **Qor tozalagich**: ekran pastida qor ko'paysa traktor / qor skuteri / belkurakli pingvin / o'zingiz tanlagan rasm chiqib, qorni suradi
- **Jonli oboy**: tungi osmon, oy, miltillovchi yulduzlar, archalar, derazalari yoritilgan kulba, tutun va miltillovchi girlyandalar.
  Tizim oboyiga tegmaydi, menyudan o'chiriladi
- Pauza, bir nechta monitor, to'liq ekranli ilovada avtomatik o'chadi, kirishda avto-ishga tushish

## Fayllar
- `Sources/Simulation.swift` — qor fizikasi, oynalarni kuzatish, uyumlar, tozalagich, sharlar/qorbobo
- `Sources/Art.swift` — qorbobo va transport vositalari chizmalari
- `Sources/Wallpaper.swift` — jonli oboy (kulba sahnasi)
- `Sources/SnowView.swift` — overlay chizish va sichqoncha
- `Sources/AppDelegate.swift` — menyu, overlay va oboy oynalari
- `Sources/main.swift` — kirish nuqtasi

## Eslatma
Kod Linux'da yozilgan va hali Xcode'da sinab ko'rilmagan. Kompilyatsiya xatosi chiqsa, xatoni yuboring.
# snow-flake-for-freee
