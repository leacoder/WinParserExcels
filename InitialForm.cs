using OfficeOpenXml;
using System.Data;

namespace WinParserExcels
{
    public partial class InitialForm : Form
    {
        public InitialForm()
        {
            InitializeComponent();
            // Configurar la licencia de EPPlus (necesario para versiones 5.0+)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        private void btnCargarExcel_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Archivos Excel (*.xlsx)|*.xlsx|Todos los archivos (*.*)|*.*";
                openFileDialog.Title = "Seleccionar archivo Excel";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        CargarExcel(openFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al cargar el archivo: {ex.Message}\n\n{ex.StackTrace}",
                                      "Error",
                                      MessageBoxButtons.OK,
                                      MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void CargarExcel(string rutaArchivo)
        {
            // Crear un DataTable para almacenar los datos originales
            DataTable dtOriginal = new DataTable();

            // Configurar FileInfo del archivo
            FileInfo fileInfo = new FileInfo(rutaArchivo);

            // Usar EPPlus para leer el archivo
            using (ExcelPackage package = new ExcelPackage(fileInfo))
            {
                // Obtener la hoja llamada "DD"
                ExcelWorksheet? worksheet = package.Workbook.Worksheets["DD"];

                // Si no existe la hoja "DD", mostrar error
                if (worksheet == null)
                {
                    MessageBox.Show("No se encontró la hoja 'DD' en el archivo Excel.",
                                  "Error",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Error);
                    return;
                }

                // Verificar que la hoja tenga contenido
                if (worksheet.Dimension == null)
                {
                    MessageBox.Show("El archivo Excel está vacío.",
                                  "Advertencia",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Warning);
                    return;
                }

                // Obtener las dimensiones de la hoja
                int rowCount = worksheet.Dimension.Rows;
                int colCount = worksheet.Dimension.Columns;

                // Combinar filas 1 y 2 para crear encabezados (nombre + apellido)
                for (int col = 1; col <= colCount; col++)
                {
                    var fila1 = worksheet.Cells[1, col].Value?.ToString() ?? "";
                    var fila2 = worksheet.Cells[2, col].Value?.ToString() ?? "";
                    string columnName = $"{fila1} {fila2}".Trim();
                    if (string.IsNullOrWhiteSpace(columnName))
                        columnName = $"Columna{col}";
                    dtOriginal.Columns.Add(columnName);
                }

                // Leer los datos desde la fila 3 en adelante (fila 3 es expedientes)
                for (int row = 3; row <= rowCount; row++)
                {
                    DataRow dataRow = dtOriginal.NewRow();
                    for (int col = 1; col <= colCount; col++)
                    {
                        var cellValue = worksheet.Cells[row, col].Value;
                        dataRow[col - 1] = cellValue ?? string.Empty;
                    }
                    dtOriginal.Rows.Add(dataRow);
                }

                // Asignar el DataTable original al primer DataGridView
                dataGridView1.DataSource = dtOriginal;
                dataGridView1.AutoResizeColumns();

                // Generar el tablero de métricas
                GenerarTablero(worksheet);

                MessageBox.Show($"Archivo cargado exitosamente.\nFilas de datos: {dtOriginal.Rows.Count}\nColumnas: {dtOriginal.Columns.Count}",
                              "Éxito",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Information);
            }
        }

        private void GenerarTablero(ExcelWorksheet worksheet)
        {
            DataTable dtTablero = new DataTable();
            dtTablero.Columns.Add("Responsable");
            dtTablero.Columns.Add("Estado");
            dtTablero.Columns.Add("Expediente");
            dtTablero.Columns.Add("Fecha Asignación Dictaminante"); // String para formato personalizado
            dtTablero.Columns.Add("Días con Dictaminante", typeof(int));
            dtTablero.Columns.Add("Fecha en Sector"); // String para formato personalizado
            dtTablero.Columns.Add("Días en Sector", typeof(int));

            int rowCount = worksheet.Dimension.Rows;

            // Determinar el estado actual
            string estadoActual = "CONFECCIÓN"; // Empezamos en confección (filas 4-9)

            for (int row = 4; row <= rowCount; row++)
            {
                var estadoCol = worksheet.Cells[row, 16].Value?.ToString(); // Columna P

                // Si encontramos un nuevo estado en la columna P, actualizamos
                if (!string.IsNullOrWhiteSpace(estadoCol) &&
                    (estadoCol.Contains("CONFECCIÓN") || estadoCol.Contains("REVISIÓN") ||
                     estadoCol.Contains("JEFATURA") || estadoCol.Contains("SUBGERENCIA") ||
                     estadoCol.Contains("GERENCIA")))
                {
                    estadoActual = estadoCol.Trim();
                }

                // Procesar cada columna (cada dictaminante) - Columnas C (3) a O (15)
                for (int col = 3; col <= 15; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Value;

                    if (cellValue != null && !string.IsNullOrWhiteSpace(cellValue.ToString()))
                    {
                        string textoCompleto = cellValue.ToString();
                        var fechas = ExtraerFechas(textoCompleto);

                        if (fechas.fechaAsignacion.HasValue)
                        {
                            string nombre = worksheet.Cells[1, col].Value?.ToString() ?? "";
                            string apellido = worksheet.Cells[2, col].Value?.ToString() ?? "";
                            string nombreCompleto = $"{nombre} {apellido}".Trim();

                            if (!string.IsNullOrWhiteSpace(nombreCompleto))
                            {
                                // Calcular días desde la asignación al dictaminante
                                int diasConDictaminante = (int)(DateTime.Now - fechas.fechaAsignacion.Value).TotalDays;

                                // Agregar una fila por cada expediente
                                DataRow rowData = dtTablero.NewRow();
                                rowData["Responsable"] = nombreCompleto;
                                rowData["Estado"] = estadoActual;
                                rowData["Expediente"] = textoCompleto;
                                rowData["Fecha Asignación Dictaminante"] = fechas.fechaAsignacion.Value.ToString("dd-MM-yy");
                                rowData["Días con Dictaminante"] = diasConDictaminante;

                                // Si existe fecha de sector, calcular días en sector
                                if (fechas.fechaSector.HasValue)
                                {
                                    int diasEnSector = (int)(DateTime.Now - fechas.fechaSector.Value).TotalDays;
                                    rowData["Fecha en Sector"] = fechas.fechaSector.Value.ToString("dd-MM-yy");
                                    rowData["Días en Sector"] = diasEnSector;
                                }
                                else
                                {
                                    rowData["Fecha en Sector"] = DBNull.Value;
                                    rowData["Días en Sector"] = DBNull.Value;
                                }

                                dtTablero.Rows.Add(rowData);
                            }
                        }
                    }
                }
            }

            // Asignar el tablero al segundo DataGridView
            dgvTablero.DataSource = dtTablero;
            dgvTablero.AutoResizeColumns();

            // Ordenar por días con dictaminante (descendente)
            dgvTablero.Sort(dgvTablero.Columns["Días con Dictaminante"], System.ComponentModel.ListSortDirection.Descending);
        }

        private (DateTime? fechaAsignacion, DateTime? fechaSector) ExtraerFechas(string texto)
        {
            try
            {
                // Buscar todas las fechas en el texto usando regex
                var patron = @"\b(\d{1,2})[/-](\d{1,2})(?:[/-](\d{2,4}))?\b";
                var matches = System.Text.RegularExpressions.Regex.Matches(texto, patron);

                if (matches.Count == 0)
                    return (null, null);

                // La primera fecha es la asignación al dictaminante
                DateTime? fechaAsignacion = ParsearFecha(matches[0].Value);

                // La última fecha es la fecha en el sector (si hay más de una)
                DateTime? fechaSector = null;
                if (matches.Count > 1)
                {
                    string ultimaFechaStr = matches[matches.Count - 1].Value;
                    fechaSector = ParsearFecha(ultimaFechaStr);

                    // Si la fecha del sector no tiene año, usar el año actual
                    if (fechaSector.HasValue && !ultimaFechaStr.Contains("/2") && !ultimaFechaStr.Contains("-2"))
                    {
                        // La fecha vino sin año (ej: "21/10"), asumimos año actual
                        fechaSector = new DateTime(DateTime.Now.Year, fechaSector.Value.Month, fechaSector.Value.Day);
                    }
                }

                return (fechaAsignacion, fechaSector);
            }
            catch
            {
                return (null, null);
            }
        }

        private DateTime? ParsearFecha(string fechaStr)
        {
            try
            {
                // Intentar parsear con barra diagonal (/) con año
                if (DateTime.TryParseExact(fechaStr, new[] { "dd/MM/yy", "d/MM/yy", "dd/MM/yyyy", "d/MM/yyyy" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime fecha))
                {
                    return fecha;
                }

                // Intentar parsear con guión (-) con año
                if (DateTime.TryParseExact(fechaStr, new[] { "dd-MM-yy", "d-MM-yy", "dd-MM-yyyy", "d-MM-yyyy" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out fecha))
                {
                    return fecha;
                }

                // Intentar parsear sin año (DD/MM o D/MM)
                if (DateTime.TryParseExact(fechaStr, new[] { "dd/MM", "d/MM", "dd-MM", "d-MM" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out fecha))
                {
                    // Asignar año actual
                    return new DateTime(DateTime.Now.Year, fecha.Month, fecha.Day);
                }
            }
            catch { }

            return null;
        }
    }
}
