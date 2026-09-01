export async function fileToBase64(file: File): Promise<string> {
  return await new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onerror = () => reject(new Error('The selected file could not be read.'))
    reader.onload = () => {
      const result = reader.result
      if (typeof result !== 'string') {
        reject(new Error('The selected file could not be read.'))
        return
      }
      const separator = result.indexOf(',')
      if (separator < 0) {
        reject(new Error('The selected file could not be encoded.'))
        return
      }
      resolve(result.slice(separator + 1))
    }
    reader.readAsDataURL(file)
  })
}

export function filePayload(file: File, content: string) {
  return {
    fileName: file.name,
    mediaType: file.type || 'application/octet-stream',
    content,
  }
}
