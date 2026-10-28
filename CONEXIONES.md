# Lista de conexiones

## Prototipo sin electrónica externa

| Origen | Destino | Función |
|---|---|---|
| PC Windows, USB-A o USB-C | USB-C del M5Stack ATOM Matrix | Alimentación de 5 V, programación y puerto serial |
| ESP32 GPIO 27 | Matriz WS2812C integrada | Datos RGB, conexión interna de fábrica |
| 5 V interno | Matriz RGB integrada | Alimentación, conexión interna de fábrica |
| GND interno | Matriz RGB integrada | Referencia eléctrica, conexión interna de fábrica |

No se requieren jumpers, protoboard, resistencias ni fuente separada para este piloto. Use un cable USB-C que soporte datos, no solo carga.

## Parámetros seriales

- 115200 baudios
- 8 bits de datos
- Sin paridad
- 1 bit de parada
- Fin de línea: `\n`
- Mensajes: JSON UTF-8, una línea por comando
