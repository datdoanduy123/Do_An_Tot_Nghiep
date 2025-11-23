// import { Injectable } from '@angular/core';
// import * as CryptoJS from 'crypto-js';

// @Injectable({
//   providedIn: 'root',
// })
// export class StorageService {
//   private secretKey = 'my-secret-key-123'; // Nên lưu ở biến môi trường nếu bảo mật cao

//   setEncrypted(key: string, value: any): void {
//     const encrypted = CryptoJS.AES.encrypt(
//       JSON.stringify(value),
//       this.secretKey
//     ).toString();
//     localStorage.setItem(key, encrypted);
//   }

//   getDecrypted<T>(key: string): T | null {
//     const encryptedData = localStorage.getItem(key);
//     if (!encryptedData) return null;

//     try {
//       const bytes = CryptoJS.AES.decrypt(encryptedData, this.secretKey);
//       const decrypted = bytes.toString(CryptoJS.enc.Utf8);

//       // Nếu decrypted rỗng hoặc null thì coi như không hợp lệ
//       if (!decrypted) {
//         console.warn('Không có dữ liệu giải mã được cho', key);
//         return null;
//       }

//       return JSON.parse(decrypted) as T;
//     } catch (err) {
//       console.error('Decryption/parse error:', err);
//       return null;
//     }
//   }

//   remove(key: string): void {
//     localStorage.removeItem(key);
//   }

//   clear(): void {
//     localStorage.clear();
//   }
// }
